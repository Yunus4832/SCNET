using System.Net;
using System.Net.Sockets;

using NetworkDamageTool;

namespace NetworkDamageTool.Test;

public sealed class DamageProxyIntegrationTest
{
    [Fact]
    public async Task ZeroDamageForwardsDatagramsInBothDirections()
    {
        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var serverEndPoint = (IPEndPoint)server.Client.LocalEndPoint!;
        var proxyPort = ReserveUdpPort();
        var noDamage = new LinkImpairmentOptions(0, 0, 0, 0);
        var options = new DamageProxyOptions(
            new IPEndPoint(IPAddress.Loopback, proxyPort),
            serverEndPoint,
            1,
            noDamage,
            noDamage,
            null,
            null);
        await using var proxy = new DamageProxy(options);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var proxyTask = proxy.RunAsync(cancellation.Token);
        using var client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var payload = new byte[] { 1, 2, 3, 4 };

        await client.SendAsync(payload, new IPEndPoint(IPAddress.Loopback, proxyPort), cancellation.Token);
        var receivedByServer = await server.ReceiveAsync(cancellation.Token);
        Assert.Equal(payload, receivedByServer.Buffer);

        var reply = new byte[] { 9, 8, 7 };
        await server.SendAsync(reply, receivedByServer.RemoteEndPoint, cancellation.Token);
        var receivedByClient = await client.ReceiveAsync(cancellation.Token);
        Assert.Equal(reply, receivedByClient.Buffer);

        cancellation.Cancel();
        await proxyTask;
    }

    [Fact]
    public async Task LatencyDoesNotSerializeIndependentDatagrams()
    {
        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var serverEndPoint = (IPEndPoint)server.Client.LocalEndPoint!;
        var proxyPort = ReserveUdpPort();
        var delayed = new LinkImpairmentOptions(100, 0, 0, 0);
        var options = new DamageProxyOptions(
            new IPEndPoint(IPAddress.Loopback, proxyPort),
            serverEndPoint,
            1,
            delayed,
            delayed,
            null,
            null);
        await using var proxy = new DamageProxy(options);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var proxyTask = proxy.RunAsync(cancellation.Token);
        using var client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var proxyEndPoint = new IPEndPoint(IPAddress.Loopback, proxyPort);
        var started = System.Diagnostics.Stopwatch.StartNew();

        for (byte value = 0; value < 5; value++)
        {
            await client.SendAsync(new[] { value }, proxyEndPoint, cancellation.Token);
        }

        for (byte value = 0; value < 5; value++)
        {
            await server.ReceiveAsync(cancellation.Token);
        }

        Assert.True(started.Elapsed < TimeSpan.FromMilliseconds(350), $"Elapsed: {started.Elapsed}");
        cancellation.Cancel();
        await proxyTask;
    }

    private static int ReserveUdpPort()
    {
        using var socket = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)socket.Client.LocalEndPoint!).Port;
    }
}
