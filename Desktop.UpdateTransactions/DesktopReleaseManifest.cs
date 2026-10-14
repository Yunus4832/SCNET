using System.Xml.Serialization;

namespace Scnet.Desktop.Updates;

public sealed class DesktopReleaseManifest
{
    public const string FileName = "DesktopRelease.xml";

    public string Platform { get; set; } = string.Empty;

    public List<string> Files { get; set; } = [];

    [XmlIgnore]
    public string Executable => Platform == "windows-x64" ? "SurvivalcraftStarter.exe" : "SurvivalcraftStarter";

    public static DesktopReleaseManifest Read(string path, string platform)
    {
        using var input = File.OpenRead(path);
        var manifest = (DesktopReleaseManifest?)new XmlSerializer(typeof(DesktopReleaseManifest)).Deserialize(input)
                       ?? throw new InvalidDataException("Missing release manifest.");
        if (manifest.Platform != platform || manifest.Files.Count == 0 ||
            !manifest.Files.Contains(manifest.Executable, StringComparer.Ordinal) ||
            !manifest.Files.Contains("Content.zip", StringComparer.Ordinal) ||
            manifest.Files.Distinct(StringComparer.OrdinalIgnoreCase).Count() != manifest.Files.Count)
        {
            throw new InvalidDataException("The release package does not match this platform.");
        }

        foreach (var file in manifest.Files)
        {
            ValidateFileName(file);
        }

        return manifest;
    }

    internal static void ValidateFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name is "." or ".." || name.Contains('/') || name.Contains('\\') ||
            name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            new[] { "Instances", "Starter.xml", ".update", "Settings.xml", "RunningSetting.xml" }
                .Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Invalid release file: {name}");
        }
    }
}
