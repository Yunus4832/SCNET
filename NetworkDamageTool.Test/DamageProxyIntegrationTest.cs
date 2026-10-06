using System.Net;
using System.Net.Sockets;

namespace NetworkDamageTool.Test;

public sealed class DamageProxyIntegrationTest
{
    [Fact]
    public async Task OutageDropsTrafficAndRecoversWithoutChangingRoute()
    {
        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var proxyEndPoint = new IPEndPoint(IPAddress.Loopback, ReserveUdpPort());
        var outage = new LinkImpairmentOptions(0, 0, 0, 0)
        {
            OutageStart = TimeSpan.FromMilliseconds(500),
            OutageDuration = TimeSpan.FromMilliseconds(700)
        };
        var noDamage = new LinkImpairmentOptions(0, 0, 0, 0);
        var options = new DamageProxyOptions(proxyEndPoint, (IPEndPoint)server.Client.LocalEndPoint!,
            1, outage, noDamage, null, null);
        await using var proxy = new DamageProxy(options);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var proxyTask = proxy.RunAsync(cancellation.Token);
        using var client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        try
        {
            await client.SendAsync(new byte[] { 1 }, proxyEndPoint, cancellation.Token);
            var first = await server.ReceiveAsync(cancellation.Token);
            await Task.Delay(600, cancellation.Token);
            await client.SendAsync(new byte[] { 2 }, proxyEndPoint, cancellation.Token);
            await Task.Delay(800, cancellation.Token);
            await client.SendAsync(new byte[] { 3 }, proxyEndPoint, cancellation.Token);
            var resumed = await server.ReceiveAsync(cancellation.Token);
            Assert.Equal(new byte[] { 3 }, resumed.Buffer);
            Assert.Equal(first.RemoteEndPoint, resumed.RemoteEndPoint);
        }
        finally
        {
            cancellation.Cancel();
            await proxyTask;
        }
    }

    [Fact]
    public async Task DiscoveryAndConnectionEndpointsKeepIndependentReplyRoutes()
    {
        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var proxyEndPoint = new IPEndPoint(IPAddress.Loopback, ReserveUdpPort());
        var noDamage = new LinkImpairmentOptions(0, 0, 0, 0);
        var options = new DamageProxyOptions(proxyEndPoint, (IPEndPoint)server.Client.LocalEndPoint!,
            1, noDamage, noDamage, null, null);
        await using var proxy = new DamageProxy(options);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var proxyTask = proxy.RunAsync(cancellation.Token);
        using var discovery = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var connection = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));

        await discovery.SendAsync(new byte[] { 1 }, proxyEndPoint, cancellation.Token);
        var discoveryRequest = await server.ReceiveAsync(cancellation.Token);
        await connection.SendAsync(new byte[] { 2 }, proxyEndPoint, cancellation.Token);
        var connectionRequest = await server.ReceiveAsync(cancellation.Token);
        Assert.NotEqual(discoveryRequest.RemoteEndPoint, connectionRequest.RemoteEndPoint);

        // A late discovery reply must not be redirected to the newer connection socket.
        await server.SendAsync(new byte[] { 22 }, connectionRequest.RemoteEndPoint, cancellation.Token);
        await server.SendAsync(new byte[] { 11 }, discoveryRequest.RemoteEndPoint, cancellation.Token);
        Assert.Equal(new byte[] { 22 }, (await connection.ReceiveAsync(cancellation.Token)).Buffer);
        Assert.Equal(new byte[] { 11 }, (await discovery.ReceiveAsync(cancellation.Token)).Buffer);

        await discovery.SendAsync(new byte[] { 3 }, proxyEndPoint, cancellation.Token);
        Assert.Equal(discoveryRequest.RemoteEndPoint, (await server.ReceiveAsync(cancellation.Token)).RemoteEndPoint);
        cancellation.Cancel();
        await proxyTask;
    }

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
