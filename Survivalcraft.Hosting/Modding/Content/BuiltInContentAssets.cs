using System.IO.Compression;

namespace Game.Modding.Content;

internal static class BuiltInContentAssets
{
    private const string _assetRoot = "Assets";

    private const string _appArchivePath = "app:Assets.zip";

    public static IReadOnlyList<ContentRegistration> Load()
    {
        if (Storage.FileExists(_appArchivePath))
        {
            return LoadFromArchive();
        }

        var assetDirectory = FindAssetDirectory();
        return assetDirectory is not null
            ? LoadFromDirectory(assetDirectory)
            : throw new FileNotFoundException("Built-in content assets were not found.");
    }

    private static IReadOnlyList<ContentRegistration> LoadFromArchive()
    {
        using var stream = Storage.OpenFile(_appArchivePath, OpenFileMode.Read);
        using var archive = new ZipArchive(
            stream,
            ZipArchiveMode.Read,
            leaveOpen: false
        );
        var assets = new List<ContentRegistration>();
        foreach (var entry in archive.Entries.Where(entry => !string.IsNullOrEmpty(entry.Name)))
        {
            using var entryStream = entry.Open();
            using var memory = new MemoryStream((int)entry.Length);
            entryStream.CopyTo(memory);
            assets.Add(new ContentRegistration(entry.FullName, memory.ToArray()));
        }

        return assets;
    }

    private static IReadOnlyList<ContentRegistration> LoadFromDirectory(string assetDirectory)
    {
        return Directory.EnumerateFiles(assetDirectory, "*", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path =>
                {
                    var relativePath = Path.GetRelativePath(assetDirectory, path)
                        .Replace(Path.DirectorySeparatorChar, '/');
                    return new ContentRegistration(relativePath, File.ReadAllBytes(path));
                }
            )
            .ToArray();
    }

    private static string? FindAssetDirectory()
    {
        foreach (var root in EnumerateSearchRoots())
        {
            var candidate = Path.Combine(root, _assetRoot);
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateSearchRoots()
    {
        if (!string.IsNullOrWhiteSpace(Directory.GetCurrentDirectory()))
        {
            yield return Directory.GetCurrentDirectory();
        }

        var baseDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        for (var current = baseDirectory; current is not null; current = current.Parent)
        {
            yield return current.FullName;
        }
    }
}
