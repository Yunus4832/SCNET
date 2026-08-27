namespace ContentServer.Controllers.Contracts.Responses;

public sealed record AdministratorInitializationResponse(
    string AdministratorId,
    string Name,
    string Status);
