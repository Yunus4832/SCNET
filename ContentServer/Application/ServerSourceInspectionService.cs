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
        var address = addresses.FirstOrDefault(PublicEndpointAddress.IsPublicAddress)
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

}
