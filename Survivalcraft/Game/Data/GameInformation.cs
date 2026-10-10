namespace Game;

public sealed record GameAnnouncement(
    string Id,
    string Title,
    string Body,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PublishedAt);

public sealed record GameRelease(
    string Id,
    string Version,
    string Platform,
    string Description,
    string DownloadUrl,
    string Sha256,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PublishedAt);
