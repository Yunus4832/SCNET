namespace SCNET.Authentication.Protocol.Contracts;

public sealed record VerifiedAuthenticationRequest<T>(
    string Subject,
    string RequestId,
    long IssuedAt,
    long ExpiresAt,
    T Data
);
