using Engine.Core.Security;

using SCNET.Authentication.Protocol.Contracts;
using SCNET.Authentication.Protocol.Http;

namespace Game.Authentication;

/// <summary>A login-once decision belongs to one live join, not to the installed registry or grant vault.</summary>
internal sealed record TemporaryJoinAuthorization(
    string Subject,
    string Server,
    string Scope,
    AuthenticationServiceEndpoint Endpoint,
    StoredAuthenticationGrant? Grant
)
{
    internal void Validate()
    {
        if (IdentityAddress.Normalize(Subject) != Subject ||
            IdentityAddress.Normalize(Server) != Server ||
            !AuthenticationJoinScope.IsValid(Scope))
        {
            throw new InvalidDataException("Invalid temporary authentication scope.");
        }

        if (Grant is { } grant &&
            (grant.Subject != Subject ||
             grant.Authority != Endpoint.Identity.Address ||
             grant.ServiceUrl != Endpoint.BaseUri.AbsoluteUri ||
             grant.Secret.Length is < 1 or > 4096 ||
             grant.Secret.Any(char.IsControl) ||
             grant.ExpiresAt <= 0 ||
             grant.ExpiresAt > DateTimeOffset.MaxValue.ToUnixTimeSeconds()))
        {
            throw new InvalidDataException("Invalid temporary authentication grant binding.");
        }
    }

    internal bool Matches(
        AdmissionChallenge challenge,
        AuthenticationServiceEndpoint endpoint,
        string scope
    )
    {
        return Subject == challenge.Subject &&
               Server == challenge.Server &&
               Scope == scope &&
               Endpoint.Identity.Address == endpoint.Identity.Address &&
               Endpoint.BaseUri == endpoint.BaseUri;
    }

    internal StoredAuthenticationGrant? ValidGrant(DateTimeOffset now)
    {
        return Grant is { } grant && grant.IsValid(now) ? grant : null;
    }

    public override string ToString() => "TemporaryJoinAuthorization { secret redacted }";
}
