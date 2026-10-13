using System.Net;
using System.Net.Sockets;
using System.Text.Json;

using Rendezvous.Protocol;

namespace ContentServer.Application;

public sealed class RendezvousInspectionClient : IDisposable
{
    private readonly HttpClient _http;

    public RendezvousInspectionClient()
    {
        _http = new HttpClient(new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseProxy = false,
            ConnectCallback = ConnectAsync
        })
        { Timeout = TimeSpan.FromSeconds(10) };
    }

    internal RendezvousInspectionClient(HttpClient http) => _http = http;

    public async Task InspectAsync(Uri controlUrl, CancellationToken cancellationToken)
    {
        if (controlUrl.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(controlUrl.UserInfo) ||
            !string.IsNullOrEmpty(controlUrl.Query) || !string.IsNullOrEmpty(controlUrl.Fragment))
        {
            throw new HttpRequestException("Invalid public signaling address.");
        }
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        using var response = await _http.GetAsync(new Uri(new Uri(controlUrl.AbsoluteUri.TrimEnd('/') + "/"), "transport"),
            HttpCompletionOption.ResponseHeadersRead, deadline.Token);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > 16384)
        {
            throw new HttpRequestException("Signaling descriptor exceeds its size limit.");
        }
        await using var stream = await response.Content.ReadAsStreamAsync(deadline.Token);
        using var data = new MemoryStream();
        var buffer = new byte[4096];
        while (true)
        {
            var count = await stream.ReadAsync(buffer, deadline.Token);
            if (count == 0)
            {
                break;
            }
            if (data.Length + count > 16384)
            {
                throw new HttpRequestException("Signaling descriptor exceeds its size limit.");
            }
            data.Write(buffer, 0, count);
        }
        var descriptor = JsonSerializer.Deserialize<RendezvousDescriptor>(data.GetBuffer().AsSpan(0, (int)data.Length),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        if (descriptor is null || descriptor.UdpPort is < 1 or > 65535 || descriptor.RoomDirectoryPath != "rooms")
        {
            throw new HttpRequestException("Signaling service does not support room discovery.");
        }
    }

    private static async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken);
        var address = addresses.FirstOrDefault(PublicEndpointAddress.IsPublicAddress)
            ?? throw new HttpRequestException("Signaling service resolves to a non-public address.");
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

    public void Dispose() => _http.Dispose();
}
