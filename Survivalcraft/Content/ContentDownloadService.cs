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
    public async Task<ContentDownloadResult> DownloadExactModAsync(ContentSourceContext context,
        string modId, string version, string packageHash, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var cached = cache.Find(packageHash);
        if (cached is not null)
        {
            ValidateCachedIdentity(cached, ContentPackageType.Mod, modId, version, packageHash);
            return new ContentDownloadResult(cached, null, true, []);
        }

        context.Configure(pool);
        var failures = new List<ContentDownloadFailure>();
        var sources = new List<ContentCatalogSource>();
        ContentServerModPackage? selectedMetadata = null;
        foreach (var candidate in context.Candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sourceId = new ContentSourceId(candidate.ScopeId, candidate.Repository.Id);
            try
            {
                using var lease = pool.Acquire(candidate.ScopeId, candidate.Repository.Id);
                var metadata = await lease.Client.FindModAsync(modId, version, cancellationToken)
                    .ConfigureAwait(false);
                if (metadata is null)
                {
                    failures.Add(new ContentDownloadFailure(sourceId, candidate.Repository.Name,
                        "The repository does not contain the required mod version."));
                    continue;
                }

                if (!string.Equals(metadata.PackageHash, packageHash, StringComparison.Ordinal))
                {
                    failures.Add(new ContentDownloadFailure(sourceId, candidate.Repository.Name,
                        "The repository version has a different PackageHash."));
                    continue;
                }

                selectedMetadata ??= metadata;
                sources.Add(new ContentCatalogSource(candidate.ScopeId, candidate.Repository.Id,
                    candidate.Repository.Name, candidate.Repository.Priority, candidate.IsSession,
                    modId, version, metadata.DownloadUrl));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                failures.Add(new ContentDownloadFailure(sourceId, candidate.Repository.Name, exception.Message));
            }
        }

        if (sources.Count == 0 || selectedMetadata is null)
        {
            throw new ContentDownloadException(failures);
        }

        var aggregatedVersion = new AggregatedContentVersion(version, packageHash,
            selectedMetadata.PackageSize, selectedMetadata.FileName, false, sources);
        var content = new AggregatedContentEntry(ContentPackageType.Mod, modId, modId, null,
            [aggregatedVersion]);
        try
        {
            var result = await DownloadAsync(context, content, aggregatedVersion, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return result with { PriorFailures = failures.Concat(result.PriorFailures).ToArray() };
        }
        catch (ContentDownloadException exception)
        {
            throw new ContentDownloadException(failures.Concat(exception.Failures).ToArray());
        }
    }

    public async Task<ContentDownloadResult> DownloadAsync(ContentSourceContext context,
        AggregatedContentEntry content, AggregatedContentVersion version, ContentSourceId? explicitSource = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(version);
        var existing = cache.Find(version.PackageHash);
        if (existing is not null)
        {
            ValidateCachedIdentity(existing, content, version);
            return new ContentDownloadResult(existing, null, true, []);
        }

        context.Configure(pool);
        var candidateOrder = context.Candidates.Select((source, index) => new
        {
            Id = new ContentSourceId(source.ScopeId, source.Repository.Id),
            Index = index
        }).ToDictionary(item => item.Id, item => item.Index);
        var sources = version.Sources
            .Where(source => candidateOrder.ContainsKey(new ContentSourceId(source.ScopeId, source.RepositoryId)))
            .Where(source => explicitSource is null ||
                             source.ScopeId == explicitSource.ScopeId &&
                             source.RepositoryId == explicitSource.RepositoryId)
            .OrderBy(source => candidateOrder[new ContentSourceId(source.ScopeId, source.RepositoryId)])
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
        ValidateCachedIdentity(entry, content.Type, content.Identifier, version.Version, version.PackageHash);
    }

    private static void ValidateCachedIdentity(ContentPackageCacheEntry entry, ContentPackageType type,
        string identifier, string version, string packageHash)
    {
        if (entry.Type != type ||
            !string.Equals(entry.Identifier, identifier, StringComparison.Ordinal) ||
            !string.Equals(entry.Version, version, StringComparison.Ordinal) ||
            !string.Equals(entry.PackageHash, packageHash, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Cached package does not match the requested content identity.");
        }
    }
}
