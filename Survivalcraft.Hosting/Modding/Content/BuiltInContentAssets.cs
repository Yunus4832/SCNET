using System.IO.Compression;

namespace Game.Modding.Content;

internal static class BuiltInContentAssets
{
    private const string _assetRoot = "Assets";

    public static IReadOnlyList<ContentRegistration> Load(bool includeClientAssets)
    {
        var sharedArchive = "app:SharedAssets.zip";
        var clientArchive = "app:ClientAssets.zip";
        if (Storage.FileExists(sharedArchive) || Storage.FileExists(clientArchive))
        {
            var assets = LoadFromArchive(sharedArchive, false);
            if (includeClientAssets) { assets.AddRange(LoadFromArchive(clientArchive, true)); }

            return assets;
        }

        var root = FindAssetDirectory() ?? throw new FileNotFoundException("Built-in asset directory was not found.");
        var shared = LoadFromDirectory(Path.Combine(root, "Shared"), false);
        if (includeClientAssets) { shared.AddRange(LoadFromDirectory(Path.Combine(root, "Client"), true)); }

        return shared;
    }

    private static List<ContentRegistration> LoadFromArchive(string path, bool clientOnly)
    {
        using var stream = Storage.OpenFile(path, OpenFileMode.Read);
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
            assets.Add(new ContentRegistration(entry.FullName, memory.ToArray()) { IsClientAsset = clientOnly });
        }

        return assets;
    }

    private static List<ContentRegistration> LoadFromDirectory(string assetDirectory, bool clientOnly)
    {
        return Directory.EnumerateFiles(assetDirectory, "*", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path =>
                {
                    var relativePath = Path.GetRelativePath(assetDirectory, path)
                        .Replace(Path.DirectorySeparatorChar, '/');
                    return new ContentRegistration(relativePath, File.ReadAllBytes(path))
                        { IsClientAsset = clientOnly };
                }
            )
            .ToList();
    }

    private static string? FindAssetDirectory()
    {
        foreach (var root in EnumerateSearchRoots())
        {
            var candidate = Path.Combine(root, _assetRoot);
            if (Directory.Exists(Path.Combine(candidate, "Shared")))
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
