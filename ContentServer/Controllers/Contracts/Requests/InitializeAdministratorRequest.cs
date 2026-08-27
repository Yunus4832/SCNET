namespace ContentServer.Controllers.Contracts.Requests;

public sealed record InitializeAdministratorRequest(
    string Name,
    string ApiKey);
