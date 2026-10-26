using ContentServer.Domain.Administration;

using NetCorePal.Extensions.Domain;

namespace ContentServer.Domain.AuthenticationServices;

public sealed record AuthenticationServiceChangedDomainEvent(
    AuthenticationServiceRegistration Registration,
    string Action,
    AdministratorId? AdministratorId,
    string? Message,
    DateTimeOffset OccurredAt
) : IDomainEvent;
