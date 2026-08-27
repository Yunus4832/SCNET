namespace ContentServer.Controllers.Contracts.Responses;

public sealed record AdministratorInitializationStatusResponse(
    bool Required,
    int ApiKeyMinimumLength,
    int ApiKeyMaximumLength,
    string ApiKeyAllowedCharacters);
