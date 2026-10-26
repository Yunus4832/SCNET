using System.Text.Json;

using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace SCNET.Authentication.Protocol.Security;

internal static class AuthenticationTokenCodec
{
    internal static readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 16,
        Converters =
        {
            new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase, false)
        }
    };

    internal static JsonWebTokenHandler CreateHandler()
    {
        return new()
        {
            SetDefaultTimesOnTokenCreation = false,
            MaximumTokenSizeInBytes = AuthenticationProtocol.MaximumTokenLength
        };
    }

    internal static async Task<JsonWebToken> ValidateAsync(
        string token,
        AuthenticationPublicKey signer,
        string issuer,
        string audience,
        string tokenType,
        int maximumLifetime,
        DateTimeOffset now
    )
    {
        ValidateEncoding(token);
        using var key = signer.CreateVerifier();
        var result = await CreateHandler().ValidateTokenAsync(
            token,
            new TokenValidationParameters
            {
                RequireSignedTokens = true,
                RequireExpirationTime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new ECDsaSecurityKey(key)
                {
                    KeyId = signer.Address,
                    CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false }
                },
                ValidAlgorithms = [SecurityAlgorithms.EcdsaSha256],
                ValidateIssuer = true,
                ValidIssuer = issuer,
                ValidateAudience = true,
                ValidAudience = audience,
                IgnoreTrailingSlashWhenValidatingAudience = false,
                ValidTypes = [tokenType],
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(AuthenticationProtocol.ClockSkewSeconds),
                LifetimeValidator = (notBefore, expires, _, _) =>
                    notBefore.HasValue &&
                    expires.HasValue &&
                    notBefore.Value <= now.UtcDateTime.AddSeconds(AuthenticationProtocol.ClockSkewSeconds) &&
                    expires.Value > now.UtcDateTime
            }
        );
        if (!result.IsValid || result.SecurityToken is not JsonWebToken validated)
        {
            // Never include the token, grants, or library exceptions containing token details.
            throw new AuthenticationProtocolException("Invalid authentication signature or claims.");
        }

        var issuedAt = ReadTime(validated, "iat");
        var expiresAt = ReadTime(validated, "exp");
        var notBeforeTime = ReadTime(validated, "nbf");
        if (validated.Kid != signer.Address ||
            validated.Typ != tokenType ||
            validated.Alg != SecurityAlgorithms.EcdsaSha256 ||
            !validated.Audiences.SequenceEqual([audience], StringComparer.Ordinal) ||
            notBeforeTime != issuedAt ||
            issuedAt > now.ToUnixTimeSeconds() + AuthenticationProtocol.ClockSkewSeconds ||
            expiresAt <= now.ToUnixTimeSeconds() ||
            expiresAt <= issuedAt ||
            issuedAt < expiresAt - maximumLifetime)
        {
            throw new AuthenticationProtocolException("Invalid authentication token lifetime or purpose.");
        }

        return validated;
    }

    internal static string ReadString(JsonWebToken token, string claim)
    {
        if (!token.TryGetPayloadValue<string>(claim, out var value) || string.IsNullOrEmpty(value))
        {
            throw new AuthenticationProtocolException("Missing required authentication claim.");
        }

        return value;
    }

    internal static long ReadTime(JsonWebToken token, string claim)
    {
        if (!token.TryGetPayloadValue<long>(claim, out var value) || value < 0)
        {
            throw new AuthenticationProtocolException("Invalid authentication time claim.");
        }

        return value;
    }

    private static void ValidateEncoding(string token)
    {
        if (string.IsNullOrEmpty(token) || token.Length > AuthenticationProtocol.MaximumTokenLength)
        {
            throw new AuthenticationProtocolException("Invalid authentication token size.");
        }

        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3) { throw new FormatException(); }

            for (var index = 0; index < parts.Length; index++)
            {
                if (parts[index].Any(value => !char.IsAsciiLetterOrDigit(value) && value is not '-' and not '_'))
                {
                    throw new FormatException();
                }

                var bytes = Base64UrlEncoder.DecodeBytes(parts[index]);
                if (Base64UrlEncoder.Encode(bytes) != parts[index]) { throw new FormatException(); }

                if (index == 2)
                {
                    if (bytes.Length != 64) { throw new FormatException(); }

                    continue;
                }

                using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 16 });
                if (document.RootElement.ValueKind != JsonValueKind.Object) { throw new FormatException(); }

                RejectDuplicateProperties(document.RootElement);
                if (index == 1)
                {
                    foreach (var claim in new[] { "iat", "nbf", "exp" })
                    {
                        if (!document.RootElement.TryGetProperty(claim, out var time) ||
                            time.ValueKind != JsonValueKind.Number ||
                            !time.TryGetInt64(out _))
                        {
                            throw new FormatException();
                        }
                    }

                    foreach (var claim in new[] { "iss", "sub", "jti", "aud" })
                    {
                        if (!document.RootElement.TryGetProperty(claim, out var value) ||
                            value.ValueKind != JsonValueKind.String)
                        {
                            throw new FormatException();
                        }
                    }
                }

                if (index == 0)
                {
                    foreach (var property in document.RootElement.EnumerateObject())
                    {
                        if (property.Name is not "alg" and not "typ" and not "kid")
                        {
                            throw new FormatException();
                        }
                    }
                }
            }
        }
        catch (Exception exception) when (exception is FormatException or JsonException or ArgumentException)
        {
            throw new AuthenticationProtocolException("Invalid authentication token encoding.");
        }
    }

    private static void RejectDuplicateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var properties = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!properties.Add(property.Name)) { throw new FormatException(); }

                RejectDuplicateProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var value in element.EnumerateArray()) { RejectDuplicateProperties(value); }
        }
    }
}
