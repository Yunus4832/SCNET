namespace SCNET.Authentication.Protocol.Contracts;

/// <summary>Public discovery is not trust; callers must pin the public key independently.</summary>
public sealed record AuthenticationServiceDescription(
    int ProtocolVersion,
    string DisplayName,
    string ServiceUrl,
    string PublicKey
);
