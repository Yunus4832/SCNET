namespace Game.Modding;

public sealed class LocalModRepository(string directoryPath)
{
    private List<LocalModPackageEntry>? _entries;

    public IReadOnlyList<LocalModPackageEntry> ListAll()
    {
        EnsureIndexed();
        return _entries!;
    }

    public LocalModPackageEntry? Find(ModPackageRequirement requirement)
    {
        EnsureIndexed();
        return _entries!.FirstOrDefault(entry =>
            string.Equals(entry.ModId, requirement.ModId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(entry.Version, requirement.Version, StringComparison.OrdinalIgnoreCase) &&
            (string.IsNullOrWhiteSpace(requirement.PackageHash) ||
             string.Equals(entry.PackageHash, requirement.PackageHash, StringComparison.OrdinalIgnoreCase)));
    }

    public LocalModPackageEntry? FindByHash(string packageHash)
    {
        EnsureIndexed();
        return _entries!.FirstOrDefault(entry =>
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

    public LocalModPackageEntry AddOrUpdatePackage(byte[] content, string? fileName = null)
    {
        var packageHash = ComputePackageHash(content, fileName ?? $"download{ModPackage.FileExtension}");
        Directory.CreateDirectory(directoryPath);
        var targetPath = !string.IsNullOrWhiteSpace(fileName)
            ? GetTargetPath(fileName)
            : GetCachePath(packageHash);
        File.WriteAllBytes(targetPath, content);
        Invalidate();
        return FindByHash(packageHash) ?? throw new InvalidOperationException(
            $"Package '{packageHash}' could not be indexed after download.");
    }

    public LocalModPackageEntry ImportPackage(string sourcePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        var packageHash = ComputePackageHash(sourcePath);
        Directory.CreateDirectory(directoryPath);
        var targetPath = GetCachePath(packageHash);
        if (!File.Exists(targetPath))
        {
            File.Copy(sourcePath, targetPath, false);
        }

        Invalidate();
        return FindByHash(packageHash) ?? throw new InvalidOperationException(
            $"Package '{packageHash}' could not be indexed after import.");
    }

    public void DeletePackage(LocalModPackageEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (File.Exists(entry.Path))
        {
            File.Delete(entry.Path);
        }

        Invalidate();
    }

    public void Invalidate()
    {
        _entries = null;
    }

    private void EnsureIndexed()
    {
        if (_entries != null)
        {
            return;
        }

        Directory.CreateDirectory(directoryPath);
        var sources = Directory.EnumerateFiles(directoryPath, ModPackage.SearchPattern, SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path => new ModPackageSource(Path.GetFileName(path), () => File.OpenRead(path)))
            .ToArray();
        var packages = ModPackageCatalog.Discover(sources);
        _entries = packages.Zip(sources, (package, source) => new LocalModPackageEntry(
                Path.Combine(directoryPath, source.Name),
                source.Name,
                package.Manifest.Id,
                package.Manifest.Version,
                package.PackageHash))
            .ToList();
    }

    public static string ComputePackageHash(string path)
    {
        using var stream = File.OpenRead(path);
        return ComputePackageHash(stream, path);
    }

    public static string ComputePackageHash(byte[] content, string source = "package.scpkg")
    {
        using var stream = new MemoryStream(content, writable: false);
        return ComputePackageHash(stream, source);
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
