using System.Text.Json.Serialization;

namespace Game.Authentication;

public sealed record StoredAuthenticationGrant(
    string Subject,
    string Authority,
    string ServiceUrl,
    [property: JsonIgnore] string Secret,
    long ExpiresAt
)
{
    public bool IsValid(DateTimeOffset now) => ExpiresAt > now.ToUnixTimeSeconds();

    public override string ToString() => "StoredAuthenticationGrant { secret redacted }";
}
