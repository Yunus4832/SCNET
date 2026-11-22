namespace Rendezvous.Protocol;

/// <summary>仅对单个邀请有效的参与者凭据。</summary>
public sealed record ParticipantRegistration(
    Guid SessionId,
    Guid ParticipantId,
    string Credential,
    DateTimeOffset ExpiresAt
);
