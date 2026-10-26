using SCNET.Authentication.Protocol.Http;

namespace Game.Authentication;

public sealed record InstalledAuthenticationService(
    AuthenticationServiceEndpoint Endpoint,
    bool Enabled,
    string Source = ""
);
