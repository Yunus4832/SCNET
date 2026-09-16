namespace ContentServer.Controllers.Contracts.Responses;

public sealed record DirectoryServerResponse(string Id, string PublisherId, string PublisherName, string Name,
    string Address, string? Description, string[] Tags, string ReviewStatus, string? ReviewMessage,
    bool IsEnabledByPublisher, bool IsSuspended, string? SuspensionReason, DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt, DateTimeOffset? ReviewedAt);
