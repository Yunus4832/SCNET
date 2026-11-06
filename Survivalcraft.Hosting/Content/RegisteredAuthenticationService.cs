namespace Game.Content;

public sealed record RegisteredAuthenticationService(
    Guid RepositoryId,
    string RepositoryName,
    string RepositoryUrl,
    string RegistrationId,
    string Name,
    string ServiceUrl,
    string? Description,
    DateTimeOffset ReviewedAt
);
