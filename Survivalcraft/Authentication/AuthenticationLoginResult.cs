using System.Text.Json.Serialization;

namespace Game.Authentication;

public sealed record AuthenticationLoginResult(
    StoredAuthenticationGrant? Grant,
    [property: JsonIgnore] string? Approval
)
{
    public override string ToString() => "AuthenticationLoginResult { credentials redacted }";
}
