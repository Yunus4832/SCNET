namespace SCNET.Authentication.Protocol.Contracts;

/// <summary>公开发现仅提供候选信息，调用方须独立固定公钥。</summary>
public sealed record AuthenticationServiceDescription(
    int ProtocolVersion,
    string DisplayName,
    string ServiceUrl,
    string PublicKey
);
