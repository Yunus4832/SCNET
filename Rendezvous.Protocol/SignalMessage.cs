namespace Rendezvous.Protocol;

/// <summary>Bounded control datagrams; tickets must never be logged.</summary>
public sealed record SignalMessage(
    string Operation,
    Guid RequestId,
    Guid SessionId = default,
    Guid ParticipantId = default,
    string Ticket = "",
    string? Endpoint = null,
    DateTimeOffset? ExpiresAt = null,
    string? Error = null,
    string? Challenge = null,
    string[]? LocalEndpoints = null);
