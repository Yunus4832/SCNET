namespace Game.Content;

public sealed record AuthenticationCatalogResult(
    IReadOnlyList<RegisteredAuthenticationService> Services,
    IReadOnlySet<Guid> UnavailableRepositories
);
