using System.Net;

namespace Game.Servers;

public static class ServerAddress
{
    public static string Normalize(string address, int defaultPort)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        if (defaultPort is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(defaultPort));
        }

        if (!Uri.TryCreate($"tcp://{address.Trim()}", UriKind.Absolute, out var uri) ||
            string.IsNullOrWhiteSpace(uri.Host) || !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.Equals(uri.AbsolutePath, "/", StringComparison.Ordinal) ||
            !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new ArgumentException("Server address is invalid.", nameof(address));
        }

        var port = uri.IsDefaultPort || uri.Port < 1 ? defaultPort : uri.Port;
        if (port is < 1 or > 65535)
        {
            throw new ArgumentException("Server port is invalid.", nameof(address));
        }

        var host = uri.HostNameType == UriHostNameType.IPv6
            ? $"[{IPAddress.Parse(uri.Host)}]"
            : uri.Host.ToLowerInvariant();
        return $"{host}:{port}";
    }
}
