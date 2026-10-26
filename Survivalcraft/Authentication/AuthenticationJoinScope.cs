using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Engine.Network;

namespace Game.Authentication;

/// <summary>Logical join target only; never includes negotiated endpoints or transport tickets.</summary>
internal static class AuthenticationJoinScope
{
    internal static string ForRemote(NetworkTarget target)
    {
        var description = target switch
        {
            AddressNetworkTarget address => JsonSerializer.Serialize(
                new { Kind = "address", Host = address.Host.ToLowerInvariant(), address.Port }
            ),
            InvitationSessionNetworkTarget invitation => JsonSerializer.Serialize(
                new { Kind = "invitation", Control = invitation.ControlAddress.AbsoluteUri, invitation.SessionId }
            ),
            _ => throw new ArgumentException(
                "Authentication recovery requires a resolved logical target.",
                nameof(target)
            )
        };
        return "remote:" + Hash(description);
    }

    internal static string ForWorld(string directory)
    {
        var path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Storage.GetSystemPath(directory)));
        return "world:" + Hash(OperatingSystem.IsWindows() ? path.ToUpperInvariant() : path);
    }

    internal static bool IsValid(string scope)
    {
        var prefix = scope.StartsWith("remote:", StringComparison.Ordinal) ? 7 :
            scope.StartsWith("world:", StringComparison.Ordinal) ? 6 : -1;
        return prefix > 0 &&
               scope.Length == prefix + 64 &&
               scope[prefix..].All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
    }

    private static string Hash(string value)
    {
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }
}
