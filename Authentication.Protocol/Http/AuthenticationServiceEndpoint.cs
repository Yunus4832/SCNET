using SCNET.Authentication.Protocol.Contracts;
using SCNET.Authentication.Protocol.Security;

namespace SCNET.Authentication.Protocol.Http;

/// <summary>Validated location and pinned identity; discovery alone does not establish trust.</summary>
public sealed class AuthenticationServiceEndpoint
{
    public AuthenticationServiceEndpoint(AuthenticationServiceDescription description)
    {
        if (description.ProtocolVersion != AuthenticationProtocol.Version ||
            string.IsNullOrWhiteSpace(description.DisplayName) ||
            description.DisplayName.Length > 128)
        {
            throw new AuthenticationProtocolException("Unsupported or invalid authentication service description.");
        }

        BaseUri = ParseBaseUri(description.ServiceUrl);
        Identity = AuthenticationPublicKey.Parse(description.PublicKey);
        Description = description with { ServiceUrl = BaseUri.AbsoluteUri, PublicKey = Identity.EncodedKey };
    }

    public AuthenticationServiceDescription Description { get; }

    public Uri BaseUri { get; }

    public AuthenticationPublicKey Identity { get; }

    public Uri GetUri(string path) => new(BaseUri, path);

    public static Uri ParseBaseUri(string url)
    {
        if (string.IsNullOrWhiteSpace(url) ||
            url.Length > 2048 ||
            !Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            uri.UserInfo.Length > 0 ||
            uri.Query.Length > 0 ||
            uri.Fragment.Length > 0 ||
            !uri.AbsolutePath.EndsWith('/'))
        {
            throw new AuthenticationProtocolException(
                "Authentication service requires an absolute HTTPS base URL ending in '/'."
            );
        }

        var canonical = new UriBuilder(uri) { Host = uri.IdnHost }.Uri;
        if (canonical.AbsoluteUri.Length > 2048)
        {
            throw new AuthenticationProtocolException("Authentication service URL exceeds its canonical size limit.");
        }

        return canonical;
    }
}
