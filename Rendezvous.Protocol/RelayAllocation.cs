namespace Rendezvous.Protocol;

public sealed record RelayAllocation(Guid SessionId, Guid ParticipantId, Guid AttemptId,
    string Endpoint, RelayRouteCredentials Credentials, DateTimeOffset ExpiresAt);
