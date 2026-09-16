using System.Net;
using System.Net.Sockets;

using ServerSource.Protocol;

namespace ContentServer.Application;

public interface IServerSourceInspectionService
{
    Task<ServerSourceSnapshot> InspectAsync(Uri apiUrl, CancellationToken cancellationToken);
}

public sealed class ServerSourceInspectionService : IServerSourceInspectionService, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ServerSourceProtocolClient _protocolClient;

    public ServerSourceInspectionService()
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 3,
            UseProxy = false,
            ConnectCallback = ConnectPublicEndpointAsync
        };
        _httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
        _protocolClient = new ServerSourceProtocolClient(_httpClient);
    }

    public Task<ServerSourceSnapshot> InspectAsync(Uri apiUrl, CancellationToken cancellationToken)
    {
        return _protocolClient.GetAllAsync(apiUrl, cancellationToken);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    private static async ValueTask<Stream> ConnectPublicEndpointAsync(SocketsHttpConnectionContext context,
        CancellationToken cancellationToken)
    {
        var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken);
        var address = addresses.FirstOrDefault(IsPublicAddress)
                      ?? throw new HttpRequestException("Server source resolves only to a non-public address.");
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

    internal static bool IsPublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            return IsPublicAddress(address.MapToIPv4());
        }

        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any) ||
            address.Equals(IPAddress.None) || address.Equals(IPAddress.IPv6None))
        {
            return false;
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            return bytes[0] != 0 && bytes[0] != 10 && bytes[0] != 127 &&
                   !(bytes[0] == 100 && bytes[1] is >= 64 and <= 127) &&
                   !(bytes[0] == 169 && bytes[1] == 254) &&
                   !(bytes[0] == 172 && bytes[1] is >= 16 and <= 31) &&
                   !(bytes[0] == 192 && bytes[1] == 0 && bytes[2] is 0 or 2) &&
                   !(bytes[0] == 192 && bytes[1] == 168) &&
                   !(bytes[0] == 198 && bytes[1] is 18 or 19) &&
                   !(bytes[0] == 198 && bytes[1] == 51 && bytes[2] == 100) &&
                   !(bytes[0] == 203 && bytes[1] == 0 && bytes[2] == 113) &&
                   !(bytes[0] >= 224);
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var bytes = address.GetAddressBytes();
            return !address.IsIPv6LinkLocal && !address.IsIPv6Multicast && !address.IsIPv6SiteLocal &&
                   !(bytes[0] is 0xfc or 0xfd) &&
                   !(bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] == 0x0d && bytes[3] == 0xb8);
        }

        return false;
    }
}
