using SCNET.Authentication.Protocol.Contracts;
using SCNET.Authentication.Protocol.Http;

namespace Game.Authentication;

/// <summary>仅登录一次的决定只在本次加入期间有效，不写入已安装服务目录或授权凭据库。</summary>
public sealed record TemporaryJoinAuthorization(
    string Subject,
    string Server,
    string Scope,
    AuthenticationServiceEndpoint Endpoint,
    StoredAuthenticationGrant? Grant
)
{
    public void Validate()
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

    public bool Matches(
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

    public StoredAuthenticationGrant? ValidGrant(DateTimeOffset now)
    {
        return Grant is { } grant && grant.IsValid(now) ? grant : null;
    }

    public override string ToString() => "TemporaryJoinAuthorization { secret redacted }";
}
