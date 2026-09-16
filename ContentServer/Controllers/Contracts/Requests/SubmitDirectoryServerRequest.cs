namespace ContentServer.Controllers.Contracts.Requests;

public sealed record SubmitDirectoryServerRequest(string Name, string Address, string? Description, string[] Tags);
