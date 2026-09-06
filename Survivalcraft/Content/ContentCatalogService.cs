using Content.Packaging;

namespace Game.Content;

public sealed class ContentCatalogService(ContentServerClientPool pool)
{
    public async Task<AggregatedContentPage> QueryAsync(Guid scopeId,
        IReadOnlyList<ContentRepository> repositories, ContentCatalogQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(repositories);
        ArgumentNullException.ThrowIfNull(query);
        query.Validate();
        var enabled = ContentRepository.NormalizeAll(repositories).Where(repository => repository.IsEnabled).ToArray();
        pool.Update(scopeId, enabled);
        var results = await Task.WhenAll(enabled.Select(repository =>
            QueryRepositoryAsync(scopeId, repository, query, cancellationToken))).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        var failures = results.Where(result => result.Failure is not null)
            .Select(result => result.Failure!)
            .OrderBy(failure => enabled.First(repository => repository.Id == failure.RepositoryId).Priority)
            .ThenBy(failure => failure.RepositoryId)
            .ToArray();
        var items = results.SelectMany(result => result.Items.Select(item => (result.Repository, Item: item)));
        return new AggregatedContentPage(Aggregate(scopeId, items), failures,
            results.Any(result => result.HasMore));
    }

    private async Task<RepositoryResult> QueryRepositoryAsync(Guid scopeId, ContentRepository repository,
        ContentCatalogQuery query, CancellationToken cancellationToken)
    {
        try
        {
            using var lease = pool.Acquire(scopeId, repository.Id);
            var page = await lease.Client.ListPageAsync(query, cancellationToken).ConfigureAwait(false);
            foreach (var item in page.Items)
            {
                _ = Validate(repository, item);
            }

            return new RepositoryResult(repository, page.Items, page.PageIndex * page.PageSize < page.Total, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new RepositoryResult(repository, [], false,
                new ContentCatalogRepositoryFailure(repository.Id, repository.Name, exception.Message));
        }
    }

    private static IReadOnlyList<AggregatedContentEntry> Aggregate(Guid scopeId,
        IEnumerable<(ContentRepository Repository, ContentCatalogItem Item)> items)
    {
        return items.Select(item => Validate(item.Repository, item.Item))
            .GroupBy(item => (item.Type, item.Identifier))
            .OrderBy(group => group.Key.Type)
            .ThenBy(group => group.Key.Identifier, StringComparer.Ordinal)
            .Select(group =>
            {
                var versions = group.GroupBy(item => (item.Item.Version, item.Item.PackageHash))
                    .Select(versionGroup => CreateVersion(scopeId, group, versionGroup))
                    .OrderByDescending(version => SemanticVersion.Parse(version.Version))
                    .ThenBy(version => version.PackageHash, StringComparer.Ordinal)
                    .ToArray();
                var metadata = group.OrderBy(item => item.Repository.Priority)
                    .ThenBy(item => item.Repository.Id).First();
                return new AggregatedContentEntry(group.Key.Type, group.Key.Identifier, metadata.Item.Name,
                    metadata.Item.Summary, versions);
            })
            .ToArray();
    }

    private static AggregatedContentVersion CreateVersion(Guid scopeId,
        IEnumerable<ValidatedItem> contentGroup, IGrouping<(string Version, string PackageHash), ValidatedItem> group)
    {
        var sources = group.OrderBy(item => item.Repository.Priority).ThenBy(item => item.Repository.Id)
            .Select(item => new ContentCatalogSource(scopeId, item.Repository.Id, item.Repository.Name,
                item.Repository.Priority, item.Item.ContentId, item.Item.VersionId, item.Item.DownloadUrl))
            .ToArray();
        var item = group.First().Item;
        var hasConflict = contentGroup.Where(candidate => candidate.Item.Version == item.Version)
            .Select(candidate => candidate.Item.PackageHash).Distinct(StringComparer.Ordinal).Skip(1).Any();
        return new AggregatedContentVersion(item.Version, item.PackageHash, item.PackageSize, item.FileName,
            hasConflict, sources);
    }

    private static ValidatedItem Validate(ContentRepository repository, ContentCatalogItem item)
    {
        if (!Enum.TryParse<ContentPackageType>(item.Type, false, out var type) || type.ToString() != item.Type ||
            string.IsNullOrWhiteSpace(item.Identifier) || item.Identifier != item.Identifier.Trim() ||
            string.IsNullOrWhiteSpace(item.Name) || string.IsNullOrWhiteSpace(item.ContentId) ||
            string.IsNullOrWhiteSpace(item.VersionId) || string.IsNullOrWhiteSpace(item.FileName) ||
            string.IsNullOrWhiteSpace(item.DownloadUrl) || item.PackageSize < 0 ||
            !SemanticVersion.TryParse(item.Version, out _) || !IsPackageHash(item.PackageHash))
        {
            throw new InvalidDataException($"Repository '{repository.Name}' returned an invalid catalog item.");
        }

        return new ValidatedItem(repository, item, type, item.Identifier);
    }

    private static bool IsPackageHash(string value)
    {
        return value.Length == 64 && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
    }

    private sealed record ValidatedItem(ContentRepository Repository, ContentCatalogItem Item,
        ContentPackageType Type, string Identifier);

    private sealed record RepositoryResult(ContentRepository Repository, IReadOnlyList<ContentCatalogItem> Items,
        bool HasMore, ContentCatalogRepositoryFailure? Failure);
}
