namespace Game.Content;

public sealed record GameAnnouncement(
    string Id,
    string Title,
    string Body,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PublishedAt
);
