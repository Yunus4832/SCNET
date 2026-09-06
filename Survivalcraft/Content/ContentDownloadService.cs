using Content.Packaging;

namespace Game.Content;

public sealed record ContentSourceId(Guid ScopeId, Guid RepositoryId);

public sealed record ContentDownloadFailure(ContentSourceId Source, string RepositoryName, string Message);

public sealed record ContentDownloadResult(
    ContentPackageCacheEntry Entry,
    ContentSourceId? Source,
    bool WasCached,
    IReadOnlyList<ContentDownloadFailure> PriorFailures);

public sealed class ContentDownloadException(IReadOnlyList<ContentDownloadFailure> failures)
    : IOException("The package could not be downloaded from any eligible content repository.")
{
    public IReadOnlyList<ContentDownloadFailure> Failures { get; } = failures;
}

public sealed class ContentDownloadService(ContentServerClientPool pool, IContentPackageCache cache)
{
    public async Task<ContentDownloadResult> DownloadAsync(AggregatedContentEntry content,
        AggregatedContentVersion version, ContentSourceId? explicitSource = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(version);
        var existing = cache.Find(version.PackageHash);
        if (existing is not null)
        {
            ValidateCachedIdentity(existing, content, version);
            return new ContentDownloadResult(existing, null, true, []);
        }

        var sources = version.Sources
            .Where(source => explicitSource is null ||
                             source.ScopeId == explicitSource.ScopeId &&
                             source.RepositoryId == explicitSource.RepositoryId)
            .OrderBy(source => source.RepositoryPriority)
            .ThenBy(source => source.ScopeId)
            .ThenBy(source => source.RepositoryId)
            .DistinctBy(source => new ContentSourceId(source.ScopeId, source.RepositoryId))
            .ToArray();
        if (sources.Length == 0)
        {
            throw new ContentDownloadException([]);
        }

        var failures = new List<ContentDownloadFailure>();
        foreach (var source in sources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sourceId = new ContentSourceId(source.ScopeId, source.RepositoryId);
            try
            {
                using var lease = pool.Acquire(source.ScopeId, source.RepositoryId);
                var item = new ContentCatalogItem
                {
                    ContentId = source.ContentId,
                    Type = content.Type.ToString(),
                    Identifier = content.Identifier,
                    Name = content.Name,
                    VersionId = source.VersionId,
                    Version = version.Version,
                    PackageHash = version.PackageHash,
                    PackageSize = version.PackageSize,
                    FileName = version.FileName,
                    DownloadUrl = source.DownloadUrl
                };
                var downloaded = await lease.Client.DownloadToCacheAsync(item, cache, cancellationToken)
                    .ConfigureAwait(false);
                ValidateCachedIdentity(downloaded, content, version);
                return new ContentDownloadResult(downloaded, sourceId, false, failures.ToArray());
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                failures.Add(new ContentDownloadFailure(sourceId, source.RepositoryName, exception.Message));
            }
        }

        throw new ContentDownloadException(failures);
    }

    private static void ValidateCachedIdentity(ContentPackageCacheEntry entry, AggregatedContentEntry content,
        AggregatedContentVersion version)
    {
        if (entry.Type != content.Type ||
            !string.Equals(entry.Identifier, content.Identifier, StringComparison.Ordinal) ||
            !string.Equals(entry.Version, version.Version, StringComparison.Ordinal) ||
            !string.Equals(entry.PackageHash, version.PackageHash, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Cached package does not match the requested content identity.");
        }
    }
}
