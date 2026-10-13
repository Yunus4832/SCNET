namespace Rendezvous.Protocol;

/// <summary>A participant credential scoped to one invitation.</summary>
public sealed record ParticipantRegistration(
    Guid SessionId,
    Guid ParticipantId,
    string Credential,
    DateTimeOffset ExpiresAt);
