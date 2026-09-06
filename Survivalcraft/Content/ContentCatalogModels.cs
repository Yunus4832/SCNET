using Content.Packaging;

namespace Game.Content;

public sealed record ContentCatalogSource(
    Guid ScopeId,
    Guid RepositoryId,
    string RepositoryName,
    int RepositoryPriority,
    string ContentId,
    string VersionId,
    string DownloadUrl);

public sealed record AggregatedContentVersion(
    string Version,
    string PackageHash,
    long PackageSize,
    string FileName,
    bool HasHashConflict,
    IReadOnlyList<ContentCatalogSource> Sources);

public sealed record AggregatedContentEntry(
    ContentPackageType Type,
    string Identifier,
    string Name,
    string? Summary,
    IReadOnlyList<AggregatedContentVersion> Versions);

public sealed record ContentCatalogRepositoryFailure(Guid RepositoryId, string RepositoryName, string Message);

public sealed record AggregatedContentPage(
    IReadOnlyList<AggregatedContentEntry> Entries,
    IReadOnlyList<ContentCatalogRepositoryFailure> Failures,
    bool HasMore);
