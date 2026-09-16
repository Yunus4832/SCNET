namespace ContentServer.Controllers.Contracts.Responses;

public sealed record ServerSourceResponse(string Id, string Name, string ApiUrl, string? Description);

public sealed record ServerSourceRegistrationResponse(string Id, string PublisherId, string PublisherName, string Name,
    string ApiUrl, string? Description, string Status, string? ReviewMessage, DateTimeOffset CreatedAt,
    DateTimeOffset? ReviewedAt);

public sealed record ServerSourceManagementResponse(string Id, string? PublisherId, string? PublisherName, string Name,
    string ApiUrl, string? Description, string Status, string? ReviewMessage, DateTimeOffset? CreatedAt,
    DateTimeOffset? ReviewedAt, bool IsBuiltIn);
