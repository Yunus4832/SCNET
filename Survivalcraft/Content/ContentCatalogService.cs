using Content.Packaging;

namespace Game.Content;

public sealed class ContentCatalogService
{
    private readonly int _maximumConcurrency;
    private readonly ContentServerClientPool _pool;

    public ContentCatalogService(ContentServerClientPool pool, int maximumConcurrency = 4)
    {
        ArgumentNullException.ThrowIfNull(pool);
        if (maximumConcurrency < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumConcurrency));
        }

        _pool = pool;
        _maximumConcurrency = maximumConcurrency;
    }

    public async Task<AggregatedContentPage> QueryAsync(ContentSourceContext context, ContentCatalogQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(query);
        query.Validate();
        context.Configure(_pool);
        using var concurrency = new SemaphoreSlim(_maximumConcurrency, _maximumConcurrency);
        var results = await Task.WhenAll(context.Candidates.Select(source =>
            QueryRepositoryAsync(source, query, concurrency, cancellationToken))).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        var failures = results.Where(result => result.Failure is not null)
            .Select(result => result.Failure!)
            .ToArray();
        var items = results.SelectMany(result => result.Items.Select(item => (result.Source, Item: item)));
        return new AggregatedContentPage(Aggregate(items), failures, results.Any(result => result.HasMore));
    }

    public async Task<AggregatedContentDetails> QueryVersionsAsync(ContentSourceContext context,
        AggregatedContentEntry content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(content);
        context.Configure(_pool);
        var sourceIds = content.Versions.SelectMany(version => version.Sources)
            .Select(source => new ContentSourceId(source.ScopeId, source.RepositoryId))
            .ToHashSet();
        using var concurrency = new SemaphoreSlim(_maximumConcurrency, _maximumConcurrency);
        var results = await Task.WhenAll(context.Candidates
            .Where(source => sourceIds.Contains(new ContentSourceId(source.ScopeId, source.Repository.Id)))
            .Select(source => QueryRepositoryVersionsAsync(source, content, concurrency, cancellationToken)))
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        var failures = results.Where(result => result.Failure is not null)
            .Select(result => result.Failure!)
            .ToArray();
        var items = results.SelectMany(result => result.Items.Select(item => (result.Source, Item: item)));
        var entry = Aggregate(items).SingleOrDefault(candidate =>
            candidate.Type == content.Type &&
            string.Equals(candidate.Identifier, content.Identifier, StringComparison.Ordinal));
        return new AggregatedContentDetails(entry is null ? content : ContentCatalogMerger.Merge(content, entry),
            failures);
    }

    private async Task<RepositoryResult> QueryRepositoryAsync(ContentSourceContext.ScopedRepository source,
        ContentCatalogQuery query, SemaphoreSlim concurrency, CancellationToken cancellationToken)
    {
        await concurrency.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var lease = _pool.Acquire(source.ScopeId, source.Repository.Id);
            var page = await lease.Client.ListPageAsync(query, cancellationToken).ConfigureAwait(false);
            foreach (var item in page.Items)
            {
                _ = Validate(source, item);
            }

            return new RepositoryResult(source, page.Items, page.PageIndex * page.PageSize < page.Total, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new RepositoryResult(source, [], false,
                new ContentCatalogRepositoryFailure(source.Repository.Id, source.Repository.Name,
                    exception.Message));
        }
        finally
        {
            concurrency.Release();
        }
    }

    private async Task<RepositoryResult> QueryRepositoryVersionsAsync(ContentSourceContext.ScopedRepository source,
        AggregatedContentEntry content, SemaphoreSlim concurrency, CancellationToken cancellationToken)
    {
        await concurrency.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var contentId = content.Versions.SelectMany(version => version.Sources)
                .Where(candidate => candidate.ScopeId == source.ScopeId &&
                                    candidate.RepositoryId == source.Repository.Id)
                .Select(candidate => candidate.ContentId)
                .Distinct(StringComparer.Ordinal)
                .Single();
            using var lease = _pool.Acquire(source.ScopeId, source.Repository.Id);
            var items = await lease.Client.ListVersionsAsync(contentId, cancellationToken).ConfigureAwait(false);
            foreach (var item in items)
            {
                var validated = Validate(source, item);
                if (validated.Type != content.Type ||
                    !string.Equals(validated.Identifier, content.Identifier, StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        $"Repository '{source.Repository.Name}' returned a version for different content.");
                }
            }

            return new RepositoryResult(source, items, false, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new RepositoryResult(source, [], false,
                new ContentCatalogRepositoryFailure(source.Repository.Id, source.Repository.Name,
                    exception.Message));
        }
        finally
        {
            concurrency.Release();
        }
    }

    private static IReadOnlyList<AggregatedContentEntry> Aggregate(
        IEnumerable<(ContentSourceContext.ScopedRepository Source, ContentCatalogItem Item)> items)
    {
        return items.Select(item => Validate(item.Source, item.Item))
            .GroupBy(item => (item.Type, item.Identifier))
            .OrderBy(group => group.Key.Type)
            .ThenBy(group => group.Key.Identifier, StringComparer.Ordinal)
            .Select(group =>
            {
                var versions = group.GroupBy(item => (item.Item.Version, item.Item.PackageHash))
                    .Select(versionGroup => CreateVersion(group, versionGroup))
                    .OrderByDescending(version => SemanticVersion.Parse(version.Version))
                    .ThenBy(version => version.PackageHash, StringComparer.Ordinal)
                    .ToArray();
                var metadata = group.OrderByDescending(item => item.Source.IsSession)
                    .ThenBy(item => item.Source.Repository.Priority)
                    .ThenBy(item => item.Source.Repository.Id).First();
                return new AggregatedContentEntry(group.Key.Type, group.Key.Identifier, metadata.Item.Name,
                    metadata.Item.Summary, versions);
            })
            .ToArray();
    }

    private static AggregatedContentVersion CreateVersion(
        IEnumerable<ValidatedItem> contentGroup, IGrouping<(string Version, string PackageHash), ValidatedItem> group)
    {
        var sources = group.OrderByDescending(item => item.Source.IsSession)
            .ThenBy(item => item.Source.Repository.Priority).ThenBy(item => item.Source.Repository.Id)
            .Select(item => new ContentCatalogSource(item.Source.ScopeId, item.Source.Repository.Id,
                item.Source.Repository.Name, item.Source.Repository.Priority, item.Source.IsSession,
                item.Item.ContentId, item.Item.VersionId, item.Item.DownloadUrl))
            .ToArray();
        var item = group.First().Item;
        var hasConflict = contentGroup.Where(candidate => candidate.Item.Version == item.Version)
            .Select(candidate => candidate.Item.PackageHash).Distinct(StringComparer.Ordinal).Skip(1).Any();
        return new AggregatedContentVersion(item.Version, item.PackageHash, item.PackageSize, item.FileName,
            hasConflict, sources);
    }

    private static ValidatedItem Validate(ContentSourceContext.ScopedRepository source, ContentCatalogItem item)
    {
        if (!Enum.TryParse<ContentPackageType>(item.Type, false, out var type) || type.ToString() != item.Type ||
            string.IsNullOrWhiteSpace(item.Identifier) || item.Identifier != item.Identifier.Trim() ||
            string.IsNullOrWhiteSpace(item.Name) || string.IsNullOrWhiteSpace(item.ContentId) ||
            string.IsNullOrWhiteSpace(item.VersionId) || string.IsNullOrWhiteSpace(item.FileName) ||
            string.IsNullOrWhiteSpace(item.DownloadUrl) || item.PackageSize < 0 ||
            !SemanticVersion.TryParse(item.Version, out _) || !IsPackageHash(item.PackageHash))
        {
            throw new InvalidDataException($"Repository '{source.Repository.Name}' returned an invalid catalog item.");
        }

        return new ValidatedItem(source, item, type, item.Identifier);
    }

    private static bool IsPackageHash(string value)
    {
        return value.Length == 64 && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
    }

    private sealed record ValidatedItem(ContentSourceContext.ScopedRepository Source, ContentCatalogItem Item,
        ContentPackageType Type, string Identifier);

    private sealed record RepositoryResult(ContentSourceContext.ScopedRepository Source,
        IReadOnlyList<ContentCatalogItem> Items,
        bool HasMore, ContentCatalogRepositoryFailure? Failure);
}
