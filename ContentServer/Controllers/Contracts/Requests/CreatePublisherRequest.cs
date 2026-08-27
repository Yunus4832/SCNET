namespace ContentServer.Controllers.Contracts.Requests;

public sealed record CreatePublisherRequest(
    string DisplayName,
    string Contact,
    string? Description);
