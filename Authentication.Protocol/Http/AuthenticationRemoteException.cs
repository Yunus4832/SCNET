using SCNET.Authentication.Protocol.Contracts;

namespace SCNET.Authentication.Protocol.Http;

public sealed class AuthenticationRemoteException(AuthenticationError error, int retryAfterSeconds = 0)
    : Exception("The authentication service rejected the request.")
{
    public AuthenticationError Error { get; } = error;

    public int RetryAfterSeconds { get; } = retryAfterSeconds;
}
