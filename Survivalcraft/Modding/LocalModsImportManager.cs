using System.Xml.Linq;

namespace Game.Modding;

public static class LocalModsImportManager
{
    public static string ImportStatePath => GamePaths.LocalModsImportStateFile;

    public static IReadOnlyList<ImportedModEntry> ListImportedMods()
    {
        return LoadState().Values
            .OrderBy(entry => entry.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static void ImportInstalledMods(
        string sourceDirectoryPath,
        string repositoryDirectoryPath,
        Action<string>? log = null)
    {
        Directory.CreateDirectory(sourceDirectoryPath);
        Directory.CreateDirectory(repositoryDirectoryPath);

        var state = LoadState();
        var repository = new LocalModRepository(repositoryDirectoryPath);
        var currentFiles = Directory
            .EnumerateFiles(sourceDirectoryPath, ModPackage.SearchPattern, SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var importedEntries = new List<ImportedModEntry>(currentFiles.Length);

        foreach (var path in currentFiles)
        {
            var fileInfo = new FileInfo(path);
            state.TryGetValue(path, out var previousEntry);
            if (CanReuse(previousEntry, fileInfo, repository))
            {
                importedEntries.Add(previousEntry!);
                continue;
            }

            var packageHash = LocalModRepository.ComputePackageHash(path);

            if (repository.FindByHash(packageHash) == null)
            {
                log?.Invoke($"导入本地模组 {fileInfo.Name}");
                repository.ImportPackage(path);
            }

            importedEntries.Add(new ImportedModEntry(
                path,
                fileInfo.Length,
                fileInfo.LastWriteTimeUtc.Ticks,
                packageHash));
        }

        SaveState(importedEntries);
    }

    public static string ExportPackage(LocalModPackageEntry entry, string targetDirectoryPath)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetDirectoryPath);
        Directory.CreateDirectory(targetDirectoryPath);

        var targetPath = Path.Combine(targetDirectoryPath, CreateExportFileName(entry));
        File.Copy(entry.Path, targetPath, true);
        RecordInstalledMod(targetPath, entry.PackageHash);
        return targetPath;
    }

    public static void RecordInstalledMod(string sourcePath, string packageHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(packageHash);

        var fileInfo = new FileInfo(sourcePath);
        var state = LoadState();
        state[sourcePath] = new ImportedModEntry(
            sourcePath,
            fileInfo.Length,
            fileInfo.LastWriteTimeUtc.Ticks,
            packageHash);
        SaveState(state.Values);
    }

    public static void RemoveInstalledMod(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return;
        }

        var state = LoadState();
        if (state.Remove(sourcePath))
        {
            SaveState(state.Values);
        }
    }

    private static bool CanReuse(ImportedModEntry? entry, FileInfo fileInfo, LocalModRepository repository)
    {
        return entry != null &&
               entry.FileSize == fileInfo.Length &&
               entry.LastWriteTimeUtcTicks == fileInfo.LastWriteTimeUtc.Ticks &&
               !string.IsNullOrWhiteSpace(entry.PackageHash) &&
               repository.FindByHash(entry.PackageHash) != null;
    }

    private static Dictionary<string, ImportedModEntry> LoadState()
    {
        var entries = new Dictionary<string, ImportedModEntry>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (!Storage.FileExists(ImportStatePath))
            {
                return entries;
            }

            using var stream = Storage.OpenFile(ImportStatePath, OpenFileMode.Read);
            var root = XElement.Load(stream);
            foreach (var element in root.Elements("Mod"))
            {
                var path = element.Attribute(nameof(ImportedModEntry.Path))?.Value;
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                entries[path] = new ImportedModEntry(
                    path,
                    ParseLong(element.Attribute(nameof(ImportedModEntry.FileSize))?.Value),
                    ParseLong(element.Attribute(nameof(ImportedModEntry.LastWriteTimeUtcTicks))?.Value),
                    element.Attribute(nameof(ImportedModEntry.PackageHash))?.Value ?? string.Empty);
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"Failed to load {ImportStatePath}: {ex.Message}");
        }

        return entries;
    }

    private static void SaveState(IEnumerable<ImportedModEntry> entries)
    {
        try
        {
            var directory = Storage.GetDirectoryName(ImportStatePath);
            if (!string.IsNullOrEmpty(directory) && !Storage.DirectoryExists(directory))
            {
                Storage.CreateDirectory(directory);
            }

            var root = new XElement("LocalModsImportState",
                entries.Select(entry => new XElement(
                        "Mod",
                        new XAttribute(nameof(ImportedModEntry.Path), entry.Path),
                        new XAttribute(nameof(ImportedModEntry.FileSize), entry.FileSize),
                        new XAttribute(nameof(ImportedModEntry.LastWriteTimeUtcTicks), entry.LastWriteTimeUtcTicks),
                        new XAttribute(nameof(ImportedModEntry.PackageHash), entry.PackageHash)
                    )
                )
            );
            using var stream = Storage.OpenFile(ImportStatePath, OpenFileMode.Create);
            root.Save(stream);
        }
        catch (Exception ex)
        {
            Log.Warning($"Failed to save {ImportStatePath}: {ex.Message}");
        }
    }

    private static long ParseLong(string? value)
    {
        return long.TryParse(value, out var parsed) ? parsed : 0L;
    }

    private static string CreateExportFileName(LocalModPackageEntry entry)
    {
        var fileName = $"{entry.ModId}-{entry.Version}{ModPackage.FileExtension}";
        foreach (var invalidChar in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(invalidChar, '_');
        }

        return fileName;
    }

    public sealed record ImportedModEntry(
        string Path,
        long FileSize,
        long LastWriteTimeUtcTicks,
        string PackageHash
    );
}
