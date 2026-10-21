namespace Rendezvous.Protocol;

/// <summary>A temporary invitation and its private host credential.</summary>
public sealed record SessionRegistration(
    Guid SessionId,
    string JoinCode,
    string HostCredential,
    DateTimeOffset ExpiresAt
);
