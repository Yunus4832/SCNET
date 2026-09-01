using Content.Packaging;

using Game.Content;

namespace Game.Modding;

public sealed class LocalModRepository(string directoryPath)
{
    private readonly ContentPackageCache _cache = new(directoryPath);

    public IReadOnlyList<LocalModPackageEntry> ListAll()
    {
        return _cache.List()
            .Where(entry => entry.Type == ContentPackageType.Mod)
            .Select(entry => new LocalModPackageEntry(entry.Path, Path.GetFileName(entry.Path),
                entry.Identifier, entry.Version, entry.PackageHash))
            .ToArray();
    }

    public LocalModPackageEntry? Find(ModPackageRequirement requirement)
    {
        return ListAll().FirstOrDefault(entry =>
            string.Equals(entry.ModId, requirement.ModId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(entry.Version, requirement.Version, StringComparison.OrdinalIgnoreCase) &&
            (string.IsNullOrWhiteSpace(requirement.PackageHash) ||
             string.Equals(entry.PackageHash, requirement.PackageHash, StringComparison.OrdinalIgnoreCase)));
    }

    public LocalModPackageEntry? FindByHash(string packageHash)
    {
        return ListAll().FirstOrDefault(entry =>
            string.Equals(entry.PackageHash, packageHash, StringComparison.OrdinalIgnoreCase));
    }

    public string GetTargetPath(string fileName)
    {
        var sanitized = Path.GetFileName(fileName);
        return Path.Combine(directoryPath, sanitized);
    }

    public string GetCachePath(string packageHash)
    {
        return Path.Combine(directoryPath, $"{packageHash.ToLowerInvariant()}{ModPackage.FileExtension}");
    }

    public LocalModPackageEntry AddPackage(Stream content)
    {
        var entry = _cache.ImportAsync(content).GetAwaiter().GetResult();
        if (entry.Type != ContentPackageType.Mod)
            throw new ContentPackageException("Only Mod packages can be added through LocalModRepository.");
        return FindByHash(entry.PackageHash)!;
    }

    public LocalModPackageEntry ImportPackage(string sourcePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        using var source = File.OpenRead(sourcePath);
        return AddPackage(source);
    }

    public void DeletePackage(LocalModPackageEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (ModPackageReferenceTracker.IsReferenced(entry))
            throw new InvalidOperationException(
                $"Mod package '{entry.ModId}@{entry.Version}' is referenced by a profile or the current runtime.");
        if (File.Exists(entry.Path))
        {
            File.Delete(entry.Path);
        }

        _cache.RebuildIndex();
    }

    public void ExportPackage(LocalModPackageEntry entry, Stream destination)
    {
        ArgumentNullException.ThrowIfNull(entry);
        _cache.ExportAsync(entry.PackageHash, destination).GetAwaiter().GetResult();
    }

    public void Invalidate()
    {
        _cache.RebuildIndex();
    }

    public static string ComputePackageHash(string path)
    {
        using var stream = File.OpenRead(path);
        return ComputePackageHash(stream, path);
    }

    public static string ComputePackageHash(Stream stream, string source)
    {
        return ModPackage.Read(source, stream).PackageHash;
    }
}

public sealed record LocalModPackageEntry(
    string Path,
    string FileName,
    string ModId,
    string Version,
    string PackageHash
);
