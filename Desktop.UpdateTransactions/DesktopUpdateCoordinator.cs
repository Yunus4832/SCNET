using System.Diagnostics;
using System.IO.Compression;

namespace Scnet.Desktop.Updates;

public static class DesktopUpdateCoordinator
{
    public static string Prepare(string archivePath, string targetDirectory, string platform)
    {
        targetDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(targetDirectory));
        DesktopUpdateProcesses.RequireOtherInstancesStopped(targetDirectory, Environment.ProcessId);
        var work = Path.Combine(targetDirectory, ".update");
        if (Directory.Exists(work))
        {
            throw new IOException("An update task already exists. Restart SCNET before trying again.");
        }

        Directory.CreateDirectory(work);
        try
        {
            using var guard = new FileStream(Path.Combine(work, "update.lock"), FileMode.CreateNew,
                FileAccess.ReadWrite, FileShare.None);
            var staged = Path.Combine(work, "staged");
            Directory.CreateDirectory(staged);
            using var archive = ZipFile.OpenRead(archivePath);
            if (archive.Entries.Count is < 2 or > 256 ||
                archive.Entries.Sum(entry => entry.Length) > 2L * 1024 * 1024 * 1024)
            {
                throw new InvalidDataException("Invalid desktop release package size.");
            }

            foreach (var entry in archive.Entries)
            {
                DesktopReleaseManifest.ValidateFileName(entry.FullName);
                if (((entry.ExternalAttributes >> 16) & 0xf000) == 0xa000)
                {
                    throw new InvalidDataException("Release packages cannot contain symbolic links.");
                }

                entry.ExtractToFile(Path.Combine(staged, entry.FullName));
            }

            var manifest = DesktopReleaseManifest.Read(Path.Combine(staged, DesktopReleaseManifest.FileName), platform);
            if (!archive.Entries.Select(entry => entry.FullName).Order(StringComparer.Ordinal)
                    .SequenceEqual(manifest.Files.Append(DesktopReleaseManifest.FileName)
                        .Order(StringComparer.Ordinal)))
            {
                throw new InvalidDataException("The release manifest does not match the archive files.");
            }

            var current = DesktopReleaseManifest.Read(Path.Combine(targetDirectory, DesktopReleaseManifest.FileName),
                platform);
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(Path.Combine(staged, manifest.Executable),
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                    UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead |
                    UnixFileMode.OtherExecute);
            }

            var task = new DesktopUpdateTask
            {
                TargetDirectory = targetDirectory,
                Platform = platform,
                State = DesktopUpdateState.Prepared,
                Files = current.Files.Concat(manifest.Files).Append(DesktopReleaseManifest.FileName)
                    .Distinct(StringComparer.Ordinal).ToList()
            };
            task.SetParent();
            var taskPath = Path.Combine(work, "task.xml");
            task.Save(taskPath);
            return taskPath;
        }
        catch
        {
            Directory.Delete(work, true);
            throw;
        }
    }

    public static void Launch(string taskPath, bool recover = false)
    {
        var task = DesktopUpdateTask.Read(taskPath);
        task.SetParent();
        task.Save(taskPath);
        var work = Path.GetDirectoryName(taskPath)!;
        var ready = Path.Combine(work, "ready");
        File.Delete(ready);
        var executable = task.Platform == "windows-x64" ? "SurvivalcraftStarter.exe" : "SurvivalcraftStarter";
        var start = new ProcessStartInfo(Path.Combine(work, "staged", executable))
        {
            WorkingDirectory = Path.Combine(work, "staged"),
            UseShellExecute = false
        };
        start.ArgumentList.Add(recover ? "--recover-update" : "--apply-update");
        start.ArgumentList.Add(taskPath);
        using var helper = Process.Start(start) ?? throw new IOException("Cannot start the update process.");
        try
        {
            var timeout = Stopwatch.StartNew();
            while (timeout.Elapsed < TimeSpan.FromSeconds(15))
            {
                if (helper.HasExited)
                {
                    throw new IOException("The update process exited before it was ready. See .update/result.log.");
                }

                if (File.Exists(ready) && int.TryParse(File.ReadAllText(ready), out var pid) && pid == helper.Id)
                {
                    return;
                }

                Thread.Sleep(50);
            }

            throw new IOException("The update process did not become ready. The installation was not changed.");
        }
        catch
        {
            if (!helper.HasExited)
            {
                helper.Kill();
                helper.WaitForExit(5000);
            }

            throw;
        }
    }

    public static bool HandleStartup(string[] args, string targetDirectory, string platform,
        Action<string, string> replace, Action<string, bool> notify)
    {
        string? taskPath = null;
        try
        {
            if (args.Length > 0 && args[0] is "--apply-update" or "--recover-update")
            {
                if (args.Length != 2)
                {
                    throw new ArgumentException("An update mode requires exactly one task file.");
                }

                taskPath = Path.GetFullPath(args[1]);
                RunHelper(taskPath, platform, args[0] == "--recover-update", replace);
                notify(args[0] == "--recover-update"
                    ? "The previous installation has been restored. Please start SCNET again."
                    : "SCNET has been updated. Please start it again.", false);
                return true;
            }

            taskPath = Path.Combine(targetDirectory, ".update", "task.xml");
            if (!File.Exists(taskPath))
            {
                var work = Path.GetDirectoryName(taskPath)!;
                if (Directory.Exists(work))
                {
                    if (Directory.Exists(Path.Combine(work, "backup")))
                    {
                        throw new InvalidDataException("The update task is missing; backup files were retained.");
                    }

                    try
                    {
                        using var guard = new FileStream(Path.Combine(work, "update.lock"), FileMode.OpenOrCreate,
                            FileAccess.ReadWrite, FileShare.None);
                    }
                    catch (IOException)
                    {
                        Console.Error.WriteLine("An update is being prepared. Wait for it to finish.");
                        return true;
                    }

                    try
                    {
                        Directory.Delete(work, true);
                    }
                    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                    {
                        Console.Error.WriteLine($"Could not clean incomplete update preparation: {exception.Message}");
                    }
                }

                return false;
            }

            var task = DesktopUpdateTask.Read(taskPath);
            if (DesktopUpdateProcesses.IsAlive(task.HelperProcessId, task.HelperStartTimeUtcTicks) ||
                DesktopUpdateProcesses.IsAlive(task.ParentProcessId, task.ParentStartTimeUtcTicks))
            {
                // Exit immediately: a Windows message box here would keep the installed executable locked.
                Console.Error.WriteLine("An update is in progress. Wait for the update process to finish.");
                return true;
            }

            if (task.State is DesktopUpdateState.Applying or DesktopUpdateState.RollbackFailed)
            {
                Launch(taskPath, true);
                return true;
            }

            // Prepared means replacement never began. Completed and RolledBack are also safe to clean.
            try
            {
                Directory.Delete(Path.GetDirectoryName(taskPath)!, true);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                Console.Error.WriteLine($"Could not clean desktop update files: {exception.Message}");
            }

            return false;
        }
        catch (Exception exception)
        {
            if (taskPath is not null && Directory.Exists(Path.GetDirectoryName(taskPath)))
            {
                try
                {
                    File.AppendAllText(Path.Combine(Path.GetDirectoryName(taskPath)!, "result.log"),
                        $"{DateTimeOffset.Now:O} {exception}\n");
                }
                catch (Exception logFailure) when (logFailure is IOException or UnauthorizedAccessException)
                {
                    Console.Error.WriteLine($"Could not write update result: {logFailure.Message}");
                }
            }

            notify("SCNET update failed. Recovery files were retained in .update.\n" + exception.Message, true);
            return true;
        }
    }

    private static void RunHelper(string taskPath, string platform, bool recover, Action<string, string> replace)
    {
        var task = DesktopUpdateTask.Read(taskPath);
        var work = Path.GetDirectoryName(taskPath)!;
        if (task.Platform != platform ||
            Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory) != Path.Combine(work, "staged"))
        {
            throw new InvalidOperationException("Update mode must run from the staged release directory.");
        }

        using var guard = new FileStream(Path.Combine(work, "update.lock"), FileMode.OpenOrCreate,
            FileAccess.ReadWrite, FileShare.None);
        using var process = Process.GetCurrentProcess();
        task.HelperProcessId = process.Id;
        task.HelperStartTimeUtcTicks = process.StartTime.ToUniversalTime().Ticks;
        task.Save(taskPath);
        File.WriteAllText(Path.Combine(work, "ready"), process.Id.ToString());
        var timeout = Stopwatch.StartNew();
        while (DesktopUpdateProcesses.IsAlive(task.ParentProcessId, task.ParentStartTimeUtcTicks))
        {
            if (timeout.Elapsed > TimeSpan.FromMinutes(1))
            {
                throw new IOException("The original process did not exit. No files were replaced.");
            }

            Thread.Sleep(100);
        }

        DesktopUpdateProcesses.RequireOtherInstancesStopped(task.TargetDirectory, Environment.ProcessId);
        if (recover)
        {
            DesktopUpdateTransaction.Rollback(taskPath, replace);
        }
        else
        {
            DesktopUpdateTransaction.Apply(taskPath, replace);
        }
    }
}
