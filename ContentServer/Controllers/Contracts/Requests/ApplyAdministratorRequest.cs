namespace ContentServer.Controllers.Contracts.Requests;

public sealed record ApplyAdministratorRequest(string Name, string Contact, string? Description);
