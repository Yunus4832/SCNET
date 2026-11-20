using System.Net;
using System.Net.Sockets;

using SCNET.Authentication.Protocol.Http;

namespace ContentServer.Application;

/// <summary>Anonymous, bounded descriptor inspection with a DNS-pinned public connection.</summary>
public sealed class AuthenticationInspectionClient
{
    private readonly Func<HttpMessageHandler> _createHandler;

    public AuthenticationInspectionClient() : this(() => new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseProxy = false,
            MaxConnectionsPerServer = 1,
            ConnectCallback = ConnectAsync
        }
    )
    {
    }

    internal AuthenticationInspectionClient(Func<HttpMessageHandler> createHandler) => _createHandler = createHandler;

    public async Task InspectAsync(Uri serviceUrl, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        await AuthenticationHttpClient.DiscoverAsync(serviceUrl.AbsoluteUri, _createHandler(), deadline.Token);
    }

    internal static IPAddress RequirePublicAddress(IPAddress[] addresses)
    {
        if (addresses.Length == 0 || addresses.Any(address => !IsPublicAddress(address)))
        {
            throw new HttpRequestException("Authentication service must resolve exclusively to public addresses.");
        }

        return addresses[0];
    }

    private static bool IsPublicAddress(IPAddress address)
    {
        if (!PublicEndpointAddress.IsPublicAddress(address)) { return false; }

        if (address.IsIPv4MappedToIPv6) { return IsPublicAddress(address.MapToIPv4()); }

        if (address.AddressFamily != AddressFamily.InterNetworkV6) { return true; }

        var bytes = address.GetAddressBytes();
        // Only global unicast, excluding transition/special ranges and documentation prefixes.
        return (bytes[0] & 0xe0) == 0x20 &&
               !(bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] < 2) &&
               !(bytes[0] == 0x20 && bytes[1] == 0x02) &&
               !(bytes[0] == 0x3f && bytes[1] == 0xff && (bytes[2] & 0xf0) == 0);
    }

    private static async ValueTask<Stream> ConnectAsync(
        SocketsHttpConnectionContext context,
        CancellationToken cancellationToken
    )
    {
        var address =
            RequirePublicAddress(await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken));
        var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        try
        {
            await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), cancellationToken);
            return new NetworkStream(socket, true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}
