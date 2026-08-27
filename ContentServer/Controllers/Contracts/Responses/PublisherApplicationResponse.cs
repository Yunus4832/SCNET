namespace ContentServer.Controllers.Contracts.Responses;

public sealed record PublisherApplicationResponse(
    string PublisherId,
    string Status,
    string ApiKey);
