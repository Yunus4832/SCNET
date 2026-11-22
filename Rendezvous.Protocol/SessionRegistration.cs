namespace Rendezvous.Protocol;

/// <summary>临时邀请及宿主的私有凭据。</summary>
public sealed record SessionRegistration(
    Guid SessionId,
    string JoinCode,
    string HostCredential,
    DateTimeOffset ExpiresAt
);
