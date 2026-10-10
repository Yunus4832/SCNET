using System.Net;

namespace Game.Servers;

public static class ServerAddress
{
    public static string Normalize(string address)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);

        if (!Uri.TryCreate($"tcp://{address.Trim()}", UriKind.Absolute, out var uri) ||
            string.IsNullOrWhiteSpace(uri.Host) || !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.Equals(uri.AbsolutePath, "/", StringComparison.Ordinal) ||
            !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new ArgumentException("Server address is invalid.", nameof(address));
        }

        if (uri.Port is 0 or > 65535)
        {
            throw new ArgumentException("Server port is invalid.", nameof(address));
        }

        var host = uri.HostNameType == UriHostNameType.IPv6
            ? $"[{IPAddress.Parse(uri.Host)}]"
            : uri.Host.ToLowerInvariant();
        return uri.Port < 1 ? host : $"{host}:{uri.Port}";
    }
}
