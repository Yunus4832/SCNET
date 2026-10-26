using System.Security.Cryptography;

using Microsoft.IdentityModel.Tokens;

namespace SCNET.Authentication.Protocol.Security;

public static class AuthenticationNonce
{
    public const int ByteLength = 32;

    public static string Create() => Encode(RandomNumberGenerator.GetBytes(ByteLength));

    public static string Encode(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != ByteLength)
        {
            throw new ArgumentException("Authentication values require 32 bytes.", nameof(bytes));
        }

        return Base64UrlEncoder.Encode(bytes.ToArray());
    }

    public static void Validate(string value)
    {
        if (string.IsNullOrEmpty(value) ||
            value.Length != 43 ||
            value.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_'))
        {
            throw new FormatException("Authentication nonces require 32 bytes encoded as base64url.");
        }

        var bytes = Base64UrlEncoder.DecodeBytes(value);
        if (bytes.Length != ByteLength || Base64UrlEncoder.Encode(bytes) != value)
        {
            throw new FormatException("Authentication nonce encoding is not canonical.");
        }
    }
}
