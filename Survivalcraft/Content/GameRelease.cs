namespace Game.Content;

public sealed record GameRelease(
    string Id,
    string Version,
    string Platform,
    string Description,
    string DownloadUrl,
    string Sha256,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PublishedAt
);
