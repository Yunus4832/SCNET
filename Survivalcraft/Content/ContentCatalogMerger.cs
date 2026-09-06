using Content.Packaging;

namespace Game.Content;

public static class ContentCatalogMerger
{
    public static AggregatedContentEntry Merge(AggregatedContentEntry first, AggregatedContentEntry second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        if (first.Type != second.Type ||
            !string.Equals(first.Identifier, second.Identifier, StringComparison.Ordinal))
        {
            throw new ArgumentException("Only entries for the same content identity can be merged.");
        }

        var allVersions = first.Versions.Concat(second.Versions).ToArray();
        var versions = allVersions
            .GroupBy(version => (version.Version, version.PackageHash))
            .Select(group =>
            {
                var version = group.Last();
                var sources = group.SelectMany(item => item.Sources)
                    .DistinctBy(source => new ContentSourceId(source.ScopeId, source.RepositoryId))
                    .OrderByDescending(source => source.IsSession)
                    .ThenBy(source => source.RepositoryPriority)
                    .ThenBy(source => source.RepositoryId)
                    .ToArray();
                var conflict = allVersions
                    .Where(item => string.Equals(item.Version, version.Version, StringComparison.Ordinal))
                    .Select(item => item.PackageHash)
                    .Distinct(StringComparer.Ordinal)
                    .Skip(1)
                    .Any();
                return version with { Sources = sources, HasHashConflict = conflict };
            })
            .OrderByDescending(version => SemanticVersion.Parse(version.Version))
            .ThenBy(version => version.PackageHash, StringComparer.Ordinal)
            .ToArray();
        return second with { Versions = versions };
    }
}
