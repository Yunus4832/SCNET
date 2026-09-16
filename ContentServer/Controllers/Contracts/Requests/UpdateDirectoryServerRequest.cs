namespace ContentServer.Controllers.Contracts.Requests;

public sealed record UpdateDirectoryServerRequest(string Name, string Address, string? Description, string[] Tags);
