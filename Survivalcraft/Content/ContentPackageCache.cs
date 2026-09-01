using Content.Packaging;

namespace Game.Content;

public sealed record ContentPackageCacheEntry(
    string Path,
    string PackageHash,
    ContentPackageType Type,
    string Identifier,
    string Name,
    string Version,
    long Size);

public interface IContentPackageCache
{
    IReadOnlyList<ContentPackageCacheEntry> List();
    ContentPackageCacheEntry? Find(string packageHash);
    Task<ContentPackageCacheEntry> ImportAsync(Stream source, CancellationToken cancellationToken = default);
    Stream OpenValidated(string packageHash);
    Task ExportAsync(string packageHash, Stream destination, CancellationToken cancellationToken = default);
    bool Delete(string packageHash);
    void RebuildIndex();
}

public sealed class ContentPackageCache(string directoryPath) : IContentPackageCache
{
    private const long _maximumPhysicalBytes = 256L * 1024 * 1024;
    private IReadOnlyList<ContentPackageCacheEntry>? _index;
    private string TemporaryDirectory => Path.Combine(directoryPath, ".temp");

    public IReadOnlyList<ContentPackageCacheEntry> List()
    {
        EnsureIndex();
        return _index!;
    }

    public ContentPackageCacheEntry? Find(string packageHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageHash);
        return List().FirstOrDefault(entry =>
            string.Equals(entry.PackageHash, packageHash, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<ContentPackageCacheEntry> ImportAsync(
        Stream source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        Directory.CreateDirectory(directoryPath);
        Directory.CreateDirectory(TemporaryDirectory);
        var temporaryPath = Path.Combine(TemporaryDirectory, $"{Guid.NewGuid():N}.upload");
        try
        {
            await using (var output = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write,
                             FileShare.None, 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                var buffer = new byte[64 * 1024];
                long total = 0;
                int read;
                while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    total += read;
                    if (total > _maximumPhysicalBytes)
                        throw new ContentPackageException("Content package exceeds the cache size limit.");
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }
                await output.FlushAsync(cancellationToken);
            }

            ContentPackageInspection inspection;
            await using (var input = File.OpenRead(temporaryPath))
                inspection = ContentPackageReader.Inspect(input);
            var targetPath = GetPath(inspection.PackageHash);
            if (File.Exists(targetPath))
            {
                await using var existing = File.OpenRead(targetPath);
                var existingInspection = ContentPackageReader.Inspect(existing);
                if (existingInspection.PackageHash != inspection.PackageHash)
                    throw new ContentPackageException("Cached package file does not match its content address.");
                File.Delete(temporaryPath);
            }
            else
            {
                File.Move(temporaryPath, targetPath);
            }
            RebuildIndex();
            return Find(inspection.PackageHash)!;
        }
        catch
        {
            File.Delete(temporaryPath);
            throw;
        }
    }

    public Stream OpenValidated(string packageHash)
    {
        var path = GetPath(packageHash);
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            64 * 1024, FileOptions.SequentialScan);
        try
        {
            var inspection = ContentPackageReader.Inspect(stream);
            if (!string.Equals(inspection.PackageHash, packageHash, StringComparison.OrdinalIgnoreCase))
                throw new ContentPackageException("Cached package file does not match its content address.");
            stream.Position = 0;
            return stream;
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    public async Task ExportAsync(
        string packageHash,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        await using var source = OpenValidated(packageHash);
        await source.CopyToAsync(destination, 64 * 1024, cancellationToken);
        await destination.FlushAsync(cancellationToken);
    }

    public bool Delete(string packageHash)
    {
        var path = GetPath(packageHash);
        if (!File.Exists(path)) return false;
        File.Delete(path);
        RebuildIndex();
        return true;
    }

    public void RebuildIndex()
    {
        _index = null;
        EnsureIndex();
    }

    private void EnsureIndex()
    {
        if (_index is not null) return;
        Directory.CreateDirectory(directoryPath);
        var entries = new List<ContentPackageCacheEntry>();
        foreach (var path in Directory.EnumerateFiles(directoryPath, $"*{ContentPackageReader.FileExtension}")
                     .OrderBy(path => path, StringComparer.Ordinal))
        {
            try
            {
                using var stream = File.OpenRead(path);
                var inspection = ContentPackageReader.Inspect(stream);
                if (!string.Equals(Path.GetFileNameWithoutExtension(path), inspection.PackageHash,
                        StringComparison.OrdinalIgnoreCase))
                    continue;
                entries.Add(ToEntry(path, inspection));
            }
            catch (ContentPackageException)
            {
                // Invalid files are quarantined logically by omitting them from the index.
            }
        }
        _index = entries;
    }

    private string GetPath(string packageHash) =>
        Path.Combine(directoryPath, packageHash.ToLowerInvariant() + ContentPackageReader.FileExtension);

    private static ContentPackageCacheEntry ToEntry(string path, ContentPackageInspection inspection) => new(
        path, inspection.PackageHash, inspection.Manifest.Type, inspection.Manifest.Identifier,
        inspection.Manifest.Name, inspection.Manifest.Version, new FileInfo(path).Length);
}
