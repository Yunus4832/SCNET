namespace SCNET.Authentication.Protocol.Contracts;

/// <summary>已验证的短期批准凭证，不能作为可复用的用户授权。</summary>
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
