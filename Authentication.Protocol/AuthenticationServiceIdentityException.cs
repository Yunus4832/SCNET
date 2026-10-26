namespace SCNET.Authentication.Protocol;

public sealed class AuthenticationServiceIdentityException()
    : AuthenticationProtocolException(
        "Authentication service identity or endpoint differs from the confirmed service."
    );
