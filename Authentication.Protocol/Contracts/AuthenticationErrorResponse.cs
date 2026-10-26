namespace SCNET.Authentication.Protocol.Contracts;

public sealed record AuthenticationErrorResponse(AuthenticationError Error, int RetryAfterSeconds = 0);
