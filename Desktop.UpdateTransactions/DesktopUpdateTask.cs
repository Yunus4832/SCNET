using System.Diagnostics;
using System.Xml.Serialization;

namespace Scnet.Desktop.Updates;

public sealed class DesktopUpdateTask
{
    public string TargetDirectory { get; set; } = string.Empty;

    public string Platform { get; set; } = string.Empty;

    public DesktopUpdateState State { get; set; }

    public List<string> Files { get; set; } = [];

    public List<string> OriginalFiles { get; set; } = [];

    public int ParentProcessId { get; set; }

    public long ParentStartTimeUtcTicks { get; set; }

    public int HelperProcessId { get; set; }

    public long HelperStartTimeUtcTicks { get; set; }

    public void SetParent()
    {
        using var process = Process.GetCurrentProcess();
        ParentProcessId = process.Id;
        ParentStartTimeUtcTicks = process.StartTime.ToUniversalTime().Ticks;
    }

    public static DesktopUpdateTask Read(string path)
    {
        using var input = File.OpenRead(path);
        var task = (DesktopUpdateTask?)new XmlSerializer(typeof(DesktopUpdateTask)).Deserialize(input) ??
                   throw new InvalidDataException("Missing update task.");
        if (!Path.IsPathFullyQualified(task.TargetDirectory) ||
            Path.GetFullPath(path) != Path.Combine(task.TargetDirectory, ".update", "task.xml") ||
            task.Platform is not ("linux-x64" or "windows-x64") ||
            !Enum.IsDefined(task.State) ||
            task.Files.Count == 0 ||
            task.Files.Distinct(StringComparer.OrdinalIgnoreCase).Count() != task.Files.Count ||
            task.OriginalFiles.Except(task.Files, StringComparer.Ordinal).Any())
        {
            throw new InvalidDataException("Invalid update task.");
        }

        foreach (var file in task.Files)
        {
            DesktopReleaseManifest.ValidateFileName(file);
        }

        return task;
    }

    public void Save(string path)
    {
        var temporary = path + ".tmp";
        using (var output = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            new XmlSerializer(typeof(DesktopUpdateTask)).Serialize(output, this);
            output.Flush(true);
        }

        File.Move(temporary, path, true);
    }
}
