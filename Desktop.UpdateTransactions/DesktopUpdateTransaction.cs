namespace Scnet.Desktop.Updates;

public static class DesktopUpdateTransaction
{
    public static void Apply(string taskPath, Action<string, string> replace)
    {
        var task = DesktopUpdateTask.Read(taskPath);
        if (task.State != DesktopUpdateState.Prepared)
        {
            throw new InvalidOperationException("The update is not prepared.");
        }

        var work = Path.GetDirectoryName(taskPath)!;
        var manifest = DesktopReleaseManifest.Read(Path.Combine(work, "staged", DesktopReleaseManifest.FileName),
            task.Platform);
        DesktopUpdateProcesses.RequireOtherInstancesStopped(task.TargetDirectory, Environment.ProcessId);
        try
        {
            Directory.CreateDirectory(Path.Combine(work, "backup"));
            task.OriginalFiles = task.Files.Where(file => File.Exists(Path.Combine(task.TargetDirectory, file))).ToList();
            foreach (var file in task.OriginalFiles)
            {
                File.Copy(Path.Combine(task.TargetDirectory, file), Path.Combine(work, "backup", file), true);
            }

            task.State = DesktopUpdateState.Applying;
            task.Save(taskPath);
            foreach (var file in task.Files)
            {
                var destination = Path.Combine(task.TargetDirectory, file);
                if (manifest.Files.Contains(file, StringComparer.Ordinal) || file == DesktopReleaseManifest.FileName)
                {
                    replace(Path.Combine(work, "staged", file), destination);
                }
                else
                {
                    File.Delete(destination);
                }
            }

            task.State = DesktopUpdateState.Completed;
            task.Save(taskPath);
        }
        catch (Exception failure)
        {
            if (task.State is DesktopUpdateState.Applying or DesktopUpdateState.Completed)
            {
                try
                {
                    Rollback(taskPath, replace);
                }
                catch (Exception rollbackFailure)
                {
                    throw new AggregateException("Update and rollback failed; recovery files were retained.",
                        failure, rollbackFailure);
                }
            }
            else if (task.State != DesktopUpdateState.Completed)
            {
                task.State = DesktopUpdateState.RolledBack;
                task.Save(taskPath);
            }

            throw;
        }
    }

    public static void Rollback(string taskPath, Action<string, string> replace)
    {
        var task = DesktopUpdateTask.Read(taskPath);
        if (task.State is not (DesktopUpdateState.Applying or DesktopUpdateState.RollbackFailed))
        {
            if (task.State == DesktopUpdateState.Prepared)
            {
                task.State = DesktopUpdateState.RolledBack;
                task.Save(taskPath);
                return;
            }

            throw new InvalidOperationException("The update does not need recovery.");
        }

        var work = Path.GetDirectoryName(taskPath)!;
        try
        {
            foreach (var file in task.Files)
            {
                var destination = Path.Combine(task.TargetDirectory, file);
                if (task.OriginalFiles.Contains(file, StringComparer.Ordinal))
                {
                    replace(Path.Combine(work, "backup", file), destination);
                }
                else
                {
                    File.Delete(destination);
                }
            }

            task.State = DesktopUpdateState.RolledBack;
            task.Save(taskPath);
        }
        catch
        {
            task.State = DesktopUpdateState.RollbackFailed;
            task.Save(taskPath);
            throw;
        }
    }
}
