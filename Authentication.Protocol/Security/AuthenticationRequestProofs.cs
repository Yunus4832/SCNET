using System.Security.Cryptography;
using System.Text.Json;

using Microsoft.IdentityModel.Tokens;

using SCNET.Authentication.Protocol.Contracts;

namespace SCNET.Authentication.Protocol.Security;

public static class AuthenticationRequestProofs
{
    public static SignedAuthenticationRequest Create<T>(
        ECDsa subject,
        string authority,
        Uri endpoint,
        T data,
        DateTimeOffset now
    )
    {
        var identity = AuthenticationPublicKey.From(subject);
        ValidateEndpoint(endpoint);
        _ = Engine.Core.Security.IdentityAddress.Decode(authority);
        var proof = AuthenticationTokenCodec.CreateHandler().CreateToken(
            new SecurityTokenDescriptor
            {
                Issuer = identity.Address,
                Audience = authority,
                TokenType = AuthenticationProtocol.RequestTokenType,
                IssuedAt = now.UtcDateTime,
                NotBefore = now.UtcDateTime,
                Expires = now.AddSeconds(AuthenticationProtocol.MaximumRequestLifetimeSeconds).UtcDateTime,
                SigningCredentials = new SigningCredentials(
                    new ECDsaSecurityKey(subject)
                    {
                        KeyId = identity.Address,
                        CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false }
                    },
                    SecurityAlgorithms.EcdsaSha256
                ),
                Claims = new Dictionary<string, object>
                {
                    ["sub"] = identity.Address,
                    ["jti"] = AuthenticationNonce.Create(),
                    ["htm"] = "POST",
                    ["htu"] = endpoint.AbsoluteUri,
                    ["data"] = JsonSerializer.SerializeToElement(data, AuthenticationTokenCodec.jsonOptions)
                }
            }
        );
        if (proof.Length > AuthenticationProtocol.MaximumTokenLength)
        {
            throw new AuthenticationProtocolException("Authentication request exceeds the token size limit.");
        }

        return new SignedAuthenticationRequest(identity.EncodedKey, proof);
    }

    public static async Task<VerifiedAuthenticationRequest<T>> ValidateAsync<T>(
        SignedAuthenticationRequest request,
        string authority,
        Uri endpoint,
        DateTimeOffset now
    )
    {
        ValidateEndpoint(endpoint);
        AuthenticationPublicKey identity;
        try { identity = AuthenticationPublicKey.Parse(request.PublicKey); }
        catch (Exception exception) when (exception is ArgumentException or FormatException or CryptographicException)
        {
            throw new AuthenticationProtocolException("Invalid request public key.");
        }

        var jwt = await AuthenticationTokenCodec.ValidateAsync(
            request.Proof,
            identity,
            identity.Address,
            authority,
            AuthenticationProtocol.RequestTokenType,
            AuthenticationProtocol.MaximumRequestLifetimeSeconds,
            now
        );
        if (jwt.Subject != identity.Address ||
            AuthenticationTokenCodec.ReadString(jwt, "htm") != "POST" ||
            AuthenticationTokenCodec.ReadString(jwt, "htu") != endpoint.AbsoluteUri)
        {
            throw new AuthenticationProtocolException("Request proof does not belong to this endpoint or subject.");
        }

        var requestId = AuthenticationTokenCodec.ReadString(jwt, "jti");
        AuthenticationNonce.Validate(requestId);
        try
        {
            if (!jwt.TryGetPayloadValue<JsonElement>("data", out var element) ||
                element.Deserialize<T>(AuthenticationTokenCodec.jsonOptions) is not { } data)
            {
                throw new AuthenticationProtocolException("Missing signed request data.");
            }

            return new VerifiedAuthenticationRequest<T>(
                identity.Address,
                requestId,
                AuthenticationTokenCodec.ReadTime(jwt, "iat"),
                AuthenticationTokenCodec.ReadTime(jwt, "exp"),
                data
            );
        }
        catch (JsonException)
        {
            throw new AuthenticationProtocolException("Invalid signed request data.");
        }
    }

    private static void ValidateEndpoint(Uri endpoint)
    {
        if (!endpoint.IsAbsoluteUri ||
            endpoint.Scheme != Uri.UriSchemeHttps ||
            endpoint.UserInfo.Length > 0 ||
            endpoint.Fragment.Length > 0 ||
            endpoint.Query.Length > 0)
        {
            throw new ArgumentException("Authentication endpoints require absolute HTTPS URLs.", nameof(endpoint));
        }
    }
}
