using System.Diagnostics;
using System.IO.Compression;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace Scnet.Desktop.Updates.Test;

public sealed class DesktopUpdateTest : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "scnet-update-test-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void AppliesReleasePreservesInstanceAndCleansOnNextStartup()
    {
        var taskPath = Prepare();
        var task = DesktopUpdateTask.Read(taskPath);
        var instance = Path.Combine(task.TargetDirectory, "Instances", "default");
        Directory.CreateDirectory(instance);
        File.WriteAllText(Path.Combine(instance, "world.xml"), "world");
        File.WriteAllText(Path.Combine(task.TargetDirectory, "Starter.xml"), "settings");
        File.WriteAllText(Path.Combine(task.TargetDirectory, "custom.txt"), "custom");
        DesktopUpdateTransaction.Apply(taskPath, Replace);

        Assert.Equal(DesktopUpdateState.Completed, DesktopUpdateTask.Read(taskPath).State);
        Assert.Equal("new", File.ReadAllText(Path.Combine(task.TargetDirectory, "Content.zip")));
        Assert.Equal("settings", File.ReadAllText(Path.Combine(task.TargetDirectory, "Starter.xml")));
        Assert.Equal("world", File.ReadAllText(Path.Combine(instance, "world.xml")));
        Assert.Equal("custom", File.ReadAllText(Path.Combine(task.TargetDirectory, "custom.txt")));
        Assert.False(File.Exists(Path.Combine(task.TargetDirectory, "obsolete.dll")));
        task = DesktopUpdateTask.Read(taskPath);
        task.ParentProcessId = 0;
        task.Save(taskPath);
        Assert.False(
            DesktopUpdateCoordinator.HandleStartup(
                [],
                task.TargetDirectory,
                "linux-x64",
                Replace,
                (_, _) => Assert.Fail("Successful cleanup should not notify.")
            )
        );
        Assert.False(Directory.Exists(Path.GetDirectoryName(taskPath)));
    }

    [Fact]
    public void ReplacementFailureRestoresOldFilesAndRemovesNewFiles()
    {
        var taskPath = Prepare();
        var task = DesktopUpdateTask.Read(taskPath);
        Assert.Throws<IOException>(() => DesktopUpdateTransaction.Apply(
                taskPath,
                (source, destination) =>
                {
                    if (source.Contains("staged") && Path.GetFileName(destination) == DesktopReleaseManifest.FileName)
                    {
                        throw new IOException("Injected replacement failure.");
                    }

                    Replace(source, destination);
                }
            )
        );

        Assert.Equal(DesktopUpdateState.RolledBack, DesktopUpdateTask.Read(taskPath).State);
        Assert.Equal("old", File.ReadAllText(Path.Combine(task.TargetDirectory, "Content.zip")));
        Assert.Equal("old", File.ReadAllText(Path.Combine(task.TargetDirectory, "SurvivalcraftStarter")));
        Assert.Equal("obsolete", File.ReadAllText(Path.Combine(task.TargetDirectory, "obsolete.dll")));
        Assert.False(File.Exists(Path.Combine(task.TargetDirectory, "added.dll")));
    }

    [Fact]
    public void InterruptedReplacementCanRecoverRepeatedlyAfterRollbackFailure()
    {
        var taskPath = Prepare();
        Assert.Throws<AggregateException>(() => DesktopUpdateTransaction.Apply(
                taskPath,
                (source, destination) =>
                {
                    if (Path.GetFileName(destination) == "Content.zip")
                    {
                        throw new IOException("Injected locked file.");
                    }

                    Replace(source, destination);
                }
            )
        );
        var task = DesktopUpdateTask.Read(taskPath);
        Assert.Equal(DesktopUpdateState.RollbackFailed, task.State);
        Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(taskPath)!, "backup", "Content.zip")));

        DesktopUpdateTransaction.Rollback(taskPath, Replace);
        Assert.Equal(DesktopUpdateState.RolledBack, DesktopUpdateTask.Read(taskPath).State);
        Assert.Equal("old", File.ReadAllText(Path.Combine(task.TargetDirectory, "Content.zip")));
    }

    [Fact]
    public void StartupDuringUpdateExitsWithoutHoldingAnInstalledProcessForNotification()
    {
        var taskPath = Prepare();
        var task = DesktopUpdateTask.Read(taskPath);
        Assert.True(
            DesktopUpdateCoordinator.HandleStartup(
                [],
                task.TargetDirectory,
                "linux-x64",
                Replace,
                (_, _) => Assert.Fail("An in-progress update must not hold a notification dialog.")
            )
        );
        Assert.True(File.Exists(taskPath));
    }

    [Fact]
    public void PreparedTaskCleanupDoesNotChangeInstallation()
    {
        var taskPath = Prepare();
        var task = DesktopUpdateTask.Read(taskPath);
        task.ParentProcessId = 0;
        task.Save(taskPath);
        Assert.False(
            DesktopUpdateCoordinator.HandleStartup(
                [],
                task.TargetDirectory,
                "linux-x64",
                Replace,
                (_, _) => Assert.Fail("Prepared cleanup should not notify.")
            )
        );
        Assert.Equal("old", File.ReadAllText(Path.Combine(task.TargetDirectory, "Content.zip")));
    }

    [Theory]
    [InlineData("../Content.zip")]
    [InlineData("Instances")]
    [InlineData("Starter.xml")]
    [InlineData("sub/file.dll")]
    public void RejectsArchivePathsAndProtectedFiles(string file)
    {
        var target = CreateInstallation();
        var archive = CreateArchive("linux-x64", file);
        Assert.Throws<InvalidDataException>(() => DesktopUpdateCoordinator.Prepare(archive, target, "linux-x64"));
        Assert.False(Directory.Exists(Path.Combine(target, ".update")));
        Assert.Equal("old", File.ReadAllText(Path.Combine(target, "Content.zip")));
    }

    [Fact]
    public void RejectsWrongPlatformAndUndeclaredFiles()
    {
        var target = CreateInstallation();
        Assert.Throws<InvalidDataException>(() => DesktopUpdateCoordinator.Prepare(
                CreateArchive("windows-x64"),
                target,
                "linux-x64"
            )
        );
        Assert.Throws<InvalidDataException>(() => DesktopUpdateCoordinator.Prepare(
                CreateArchive("linux-x64", "extra.dll"),
                target,
                "linux-x64"
            )
        );
    }

    [Fact]
    public void LiveOtherInstancePreventsUpdate()
    {
        var target = CreateInstallation();
        using var process = Process.GetCurrentProcess();
        var runtime = Path.Combine(target, "Instances", "other", ".runtime");
        Directory.CreateDirectory(runtime);
        new XElement(
                "InstanceProcess",
                new XAttribute("Pid", process.Id),
                new XAttribute("StartTimeUtcTicks", process.StartTime.ToUniversalTime().Ticks)
            )
            .Save(Path.Combine(runtime, "test.xml"));
        Assert.Throws<IOException>(() => DesktopUpdateProcesses.RequireOtherInstancesStopped(target, 0));
    }

    [Fact]
    public void InterruptedPreparationWithoutTaskIsCleanedBeforeNormalStartup()
    {
        var target = CreateInstallation();
        var staged = Path.Combine(target, ".update", "staged");
        Directory.CreateDirectory(staged);
        File.WriteAllText(Path.Combine(staged, "partial.dll"), "partial");
        Assert.False(
            DesktopUpdateCoordinator.HandleStartup(
                [],
                target,
                "linux-x64",
                Replace,
                (_, _) => Assert.Fail("Incomplete preparation has not modified the installation.")
            )
        );
        Assert.False(Directory.Exists(Path.Combine(target, ".update")));
        Assert.Equal("old", File.ReadAllText(Path.Combine(target, "Content.zip")));
    }

    [Fact]
    public void InvalidTaskDoesNotDeleteRecoveryFiles()
    {
        var target = CreateInstallation();
        var work = Path.Combine(target, ".update");
        Directory.CreateDirectory(work);
        File.WriteAllText(Path.Combine(work, "task.xml"), "invalid");
        var notified = false;
        Assert.True(
            DesktopUpdateCoordinator.HandleStartup(
                [],
                target,
                "linux-x64",
                Replace,
                (_, error) => notified = error
            )
        );
        Assert.True(notified);
        Assert.True(Directory.Exists(work));
    }

    private string Prepare()
    {
        var target = CreateInstallation();
        return DesktopUpdateCoordinator.Prepare(CreateArchive("linux-x64"), target, "linux-x64");
    }

    private string CreateInstallation()
    {
        var target = Path.Combine(_root, "installed");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "SurvivalcraftStarter"), "old");
        File.WriteAllText(Path.Combine(target, "Content.zip"), "old");
        File.WriteAllText(Path.Combine(target, "obsolete.dll"), "obsolete");
        WriteManifest(
            Path.Combine(target, DesktopReleaseManifest.FileName),
            "linux-x64",
            ["SurvivalcraftStarter", "Content.zip", "obsolete.dll"]
        );
        return target;
    }

    private string CreateArchive(string platform, string? extra = null)
    {
        var path = Path.Combine(_root, Guid.NewGuid().ToString("N") + ".zip");
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var file in new[] { "SurvivalcraftStarter", "Content.zip", "added.dll" })
        {
            using var writer = new StreamWriter(zip.CreateEntry(file).Open());
            writer.Write("new");
        }

        using (var writer = new StreamWriter(zip.CreateEntry(DesktopReleaseManifest.FileName).Open()))
        {
            new XmlSerializer(typeof(DesktopReleaseManifest)).Serialize(
                writer,
                new DesktopReleaseManifest
                {
                    Platform = platform,
                    Files = ["SurvivalcraftStarter", "Content.zip", "added.dll"]
                }
            );
        }

        if (extra is not null)
        {
            using var writer = new StreamWriter(zip.CreateEntry(extra).Open());
            writer.Write("extra");
        }

        return path;
    }

    private static void WriteManifest(string path, string platform, List<string> files)
    {
        using var output = File.Create(path);
        new XmlSerializer(typeof(DesktopReleaseManifest)).Serialize(
            output,
            new DesktopReleaseManifest { Platform = platform, Files = files }
        );
    }

    private static void Replace(string source, string destination)
    {
        var temporary = destination + ".new";
        File.Copy(source, temporary, true);
        File.Move(temporary, destination, true);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }
}
