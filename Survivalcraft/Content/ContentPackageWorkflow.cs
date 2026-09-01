namespace Game.Content;

public sealed record ContentPackageImportResult(ContentPackageCacheEntry CacheEntry, ContentInstallResult Installation);

public static class ContentPackageWorkflow
{
    public static async Task<ContentPackageImportResult> ImportAndInstallAsync(Stream source,
        IContentPackageCache cache, ContentInstallOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var entry = await cache.ImportAsync(source, cancellationToken);
        var installation = InstallCached(cache, entry.PackageHash, options);
        return new ContentPackageImportResult(entry, installation);
    }

    public static ContentInstallResult InstallCached(IContentPackageCache cache, string packageHash,
        ContentInstallOptions? options = null)
    {
        using var package = cache.OpenValidated(packageHash);
        return ContentInstallationManager.Install(package, options);
    }
}
