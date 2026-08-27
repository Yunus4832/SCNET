namespace ContentServer.Controllers.Contracts.Responses;

public sealed record ContentItemResponse(
    string ContentId,
    string PublisherId,
    string Type,
    string Identifier,
    string Name,
    string? Summary,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);
