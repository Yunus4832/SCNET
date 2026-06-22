using System.Text.Json;

using Microsoft.Extensions.Options;

namespace ModServer;

public sealed class ModRepositoryStore
{
    private readonly SemaphoreSlim _mutex = new(1, 1);

    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _dataDirectory;
    private readonly string _packagesDirectory;
    private readonly string _indexPath;

    public ModRepositoryStore(IOptions<ModServerOptions> options)
    {
        _dataDirectory = Path.GetFullPath(options.Value.DataDirectory);
        _packagesDirectory = Path.Combine(_dataDirectory, "packages");
        _indexPath = Path.Combine(_dataDirectory, "index.json");
        Directory.CreateDirectory(_packagesDirectory);
    }

    public async Task<IReadOnlyList<ModPackageRecord>> ListAllAsync(CancellationToken cancellationToken)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            return (await LoadIndexAsync(cancellationToken)).Packages.ToArray();
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<IReadOnlyList<ModPackageRecord>> ListByModIdAsync(string modId,
        CancellationToken cancellationToken)
    {
        var packages = await ListAllAsync(cancellationToken);
        return packages
            .Where(record => string.Equals(record.ModId, modId, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    public async Task<ModPackageRecord?> FindByVersionAsync(string modId, string version,
        CancellationToken cancellationToken)
    {
        var packages = await ListAllAsync(cancellationToken);
        return packages.FirstOrDefault(record =>
            string.Equals(record.ModId, modId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(record.Version, version, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<ModPackageRecord?> FindByHashAsync(string packageHash, CancellationToken cancellationToken)
    {
        var packages = await ListAllAsync(cancellationToken);
        return packages.FirstOrDefault(record =>
            string.Equals(record.PackageHash, packageHash, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<Stream> OpenPackageAsync(ModPackageRecord record, CancellationToken cancellationToken)
    {
        var path = GetPackagePath(record.PackageHash);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Package file was not found for hash '{record.PackageHash}'.", path);
        }

        return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous);
    }

    public async Task<SavePackageResult> SavePackageAsync(
        ModPackageRecord record,
        byte[] content,
        bool replace,
        CancellationToken cancellationToken)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            var index = await LoadIndexAsync(cancellationToken);
            var existing = index.Packages.FirstOrDefault(item =>
                string.Equals(item.ModId, record.ModId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item.Version, record.Version, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                if (!string.Equals(existing.PackageHash, record.PackageHash, StringComparison.OrdinalIgnoreCase))
                {
                    if (!replace)
                    {
                        return new SavePackageResult(SavePackageStatus.Conflict, existing);
                    }

                    await WritePackageIfMissingAsync(record.PackageHash, content, cancellationToken);
                    index.Packages.Remove(existing);
                    index.Packages.Add(record);
                    await SaveIndexAsync(index, cancellationToken);
                    DeletePackageIfUnreferenced(existing.PackageHash, index);
                    return new SavePackageResult(SavePackageStatus.Replaced, record);
                }

                return new SavePackageResult(SavePackageStatus.Unchanged, existing);
            }

            await WritePackageIfMissingAsync(record.PackageHash, content, cancellationToken);
            index.Packages.Add(record);
            await SaveIndexAsync(index, cancellationToken);
            return new SavePackageResult(SavePackageStatus.Created, record);
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<ModPackageRecord?> DeletePackageAsync(
        string modId,
        string version,
        CancellationToken cancellationToken)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            var index = await LoadIndexAsync(cancellationToken);
            var existing = index.Packages.FirstOrDefault(item =>
                string.Equals(item.ModId, modId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item.Version, version, StringComparison.OrdinalIgnoreCase));

            if (existing == null)
            {
                return null;
            }

            index.Packages.Remove(existing);
            await SaveIndexAsync(index, cancellationToken);
            DeletePackageIfUnreferenced(existing.PackageHash, index);
            return existing;
        }
        finally
        {
            _mutex.Release();
        }
    }

    private async Task<ModRepositoryIndex> LoadIndexAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_indexPath))
        {
            return new ModRepositoryIndex();
        }

        await using var stream = new FileStream(_indexPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920,
            FileOptions.Asynchronous);
        var index = await JsonSerializer.DeserializeAsync<ModRepositoryIndex>(stream, _jsonOptions, cancellationToken);
        return index ?? new ModRepositoryIndex();
    }

    private async Task SaveIndexAsync(ModRepositoryIndex index, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_dataDirectory);
        await using var stream = new FileStream(_indexPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920,
            FileOptions.Asynchronous);
        await JsonSerializer.SerializeAsync(stream, index, _jsonOptions, cancellationToken);
    }

    private async Task WritePackageIfMissingAsync(
        string packageHash,
        byte[] content,
        CancellationToken cancellationToken)
    {
        var packagePath = GetPackagePath(packageHash);
        if (!File.Exists(packagePath))
        {
            await File.WriteAllBytesAsync(packagePath, content, cancellationToken);
        }
    }

    private void DeletePackageIfUnreferenced(string packageHash, ModRepositoryIndex index)
    {
        if (index.Packages.Any(item => string.Equals(item.PackageHash, packageHash, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        var packagePath = GetPackagePath(packageHash);
        if (File.Exists(packagePath))
        {
            File.Delete(packagePath);
        }
    }

    private string GetPackagePath(string packageHash)
    {
        return Path.Combine(_packagesDirectory, $"{packageHash}.scpak");
    }
}
