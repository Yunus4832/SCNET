namespace SCNET.Authentication.Server.Domain;

public sealed record ProviderGrant(string Subject, string Account, long ExpiresAt, bool Revoked = false);
