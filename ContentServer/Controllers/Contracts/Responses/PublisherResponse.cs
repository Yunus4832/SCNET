namespace ContentServer.Controllers.Contracts.Responses;

public sealed record PublisherResponse(
    string PublisherId,
    string DisplayName,
    string Contact,
    string? Description,
    string Status,
    bool HasActiveKey,
    string? ReviewMessage,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReviewedAt
);
