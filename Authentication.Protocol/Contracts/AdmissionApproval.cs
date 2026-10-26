namespace SCNET.Authentication.Protocol.Contracts;

/// <summary>Validated short-lived approval, never a reusable user grant.</summary>
public sealed record AdmissionApproval(
    string Authority,
    string Subject,
    string Server,
    string Nonce,
    string ChannelBinding,
    string TokenId,
    long IssuedAt,
    long ExpiresAt
);
