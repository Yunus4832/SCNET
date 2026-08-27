namespace ContentServer.Controllers.Contracts.Responses;

public sealed record ContentVersionResponse(
    string ContentId,
    string PublisherId,
    string Type,
    string Identifier,
    string Name,
    string? Summary,
    string ContentStatus,
    string VersionId,
    string Version,
    string PackageHash,
    long PackageSize,
    string FileName,
    string? MetadataJson,
    string Status,
    string? ReviewMessage,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PublishedAt,
    string DownloadUrl);
