using System.Security.Cryptography;
using System.Text;

namespace ContentServer.Utils;

public static class ApiKeyUtility
{
    public const int MinimumLength = 16;

    public const int MaximumLength = 128;

    public const string AllowedCharacters = "A-Z a-z 0-9 . _ ~ -";

    public static string GeneratePublisherKey() =>
        $"scpub_{Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant()}";

    public static string GenerateAdministratorKey() =>
        $"scadm_{Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant()}";

    public static string Hash(string key) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))).ToLowerInvariant();

    public static string GetPrefix(string key) => key[..Math.Min(key.Length, 18)];

    public static bool IsValid(string? key)
    {
        return key is not null && key.Length is >= MinimumLength and <= MaximumLength &&
               key.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '~' or '-');
    }
}
