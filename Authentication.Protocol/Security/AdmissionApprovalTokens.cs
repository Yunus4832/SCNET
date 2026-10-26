using System.Security.Cryptography;

using Microsoft.IdentityModel.Tokens;

using SCNET.Authentication.Protocol.Contracts;

namespace SCNET.Authentication.Protocol.Security;

public static class AdmissionApprovalTokens
{
    public static string Issue(ECDsa authority, AdmissionChallenge challenge, DateTimeOffset now)
    {
        challenge.Validate(now);
        var identity = AuthenticationPublicKey.From(authority);
        return AuthenticationTokenCodec.CreateHandler().CreateToken(
            new SecurityTokenDescriptor
            {
                Issuer = identity.Address,
                Audience = challenge.Server,
                TokenType = AuthenticationProtocol.ApprovalTokenType,
                IssuedAt = now.UtcDateTime,
                NotBefore = now.UtcDateTime,
                Expires = DateTimeOffset.FromUnixTimeSeconds(
                    Math.Min(
                        challenge.ExpiresAt,
                        now.ToUnixTimeSeconds() + AuthenticationProtocol.MaximumApprovalLifetimeSeconds
                    )
                ).UtcDateTime,
                SigningCredentials = new SigningCredentials(
                    new ECDsaSecurityKey(authority)
                    {
                        KeyId = identity.Address,
                        CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false }
                    },
                    SecurityAlgorithms.EcdsaSha256
                ),
                Claims = new Dictionary<string, object>
                {
                    ["sub"] = challenge.Subject,
                    ["jti"] = AuthenticationNonce.Create(),
                    ["purpose"] = "join",
                    ["nonce"] = challenge.Nonce,
                    ["cb"] = challenge.ChannelBinding
                }
            }
        );
    }

    public static async Task<AdmissionApproval> ValidateAsync(
        string token,
        AuthenticationPublicKey authority,
        AdmissionChallenge expected,
        DateTimeOffset now
    )
    {
        expected.Validate(now);
        var jwt = await AuthenticationTokenCodec.ValidateAsync(
            token,
            authority,
            authority.Address,
            expected.Server,
            AuthenticationProtocol.ApprovalTokenType,
            AuthenticationProtocol.MaximumApprovalLifetimeSeconds,
            now
        );
        if (jwt.Subject != expected.Subject ||
            AuthenticationTokenCodec.ReadString(jwt, "purpose") != "join" ||
            AuthenticationTokenCodec.ReadString(jwt, "nonce") != expected.Nonce ||
            AuthenticationTokenCodec.ReadString(jwt, "cb") != expected.ChannelBinding ||
            AuthenticationTokenCodec.ReadTime(jwt, "exp") > expected.ExpiresAt)
        {
            throw new AuthenticationProtocolException("Approval does not belong to this admission attempt.");
        }

        var tokenId = AuthenticationTokenCodec.ReadString(jwt, "jti");
        AuthenticationNonce.Validate(tokenId);
        return new AdmissionApproval(
            authority.Address,
            expected.Subject,
            expected.Server,
            expected.Nonce,
            expected.ChannelBinding,
            tokenId,
            AuthenticationTokenCodec.ReadTime(jwt, "iat"),
            AuthenticationTokenCodec.ReadTime(jwt, "exp")
        );
    }
}
