using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;

using Engine.Network;

using Game.Network;
using Game.Network.Handling;
using Game.Network.Snapshots;
using Game.Network.Transport;

namespace Survivalcraft.Hosting.Test.Network;

public sealed class NetNodeLifetimeTest
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StopCancelsPendingConnectionAndResetsEvenWhenPathReleaseFails(bool releaseFails)
    {
        var node = new NetNode(NetNodeOptions.Default, false, PackageHandlerCatalog.Empty, NodeRole.Client);
        var runtime = node.Runtime;
        using var cancellation = new CancellationTokenSource();
        var connecting =
            new TaskCompletionSource<NetworkConnection>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = cancellation.Token.Register(() => connecting.TrySetCanceled(cancellation.Token));
        SetField(node, "_connectingCancellation", cancellation);
        SetField(node, "_connecting", connecting.Task);
        SetField(
            node,
            "_pathClosing",
            releaseFails ? Task.FromException(new IOException("Release failed.")) : Task.CompletedTask
        );

        node.StopImmediate();

        Assert.True(connecting.Task.IsCanceled);
        Assert.True(runtime.IsClosed);
        Assert.Equal(NodeRole.Inactive, node.Role);
        Assert.Equal(NetNode.Stage.NotConnected, node.CurrentStage);
        Assert.Null(GetField(node, "_connecting"));
        Assert.Null(GetField(node, "_pathClosing"));
        Assert.Null(GetField(node, "_connectingCancellation"));
        node.StopImmediate();
        Assert.Equal(NodeRole.Inactive, node.Role);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualTransportDisconnectNotifiesOnlyRegisteredLifetimeOnce(bool registered)
    {
        await using var server = new NetworkHost();
        await using var remote = new NetworkHost();
        var node = new NetNode(NetNodeOptions.Default, false, PackageHandlerCatalog.Empty, NodeRole.Authority);
        var disconnect = DisconnectHandler(node);
        server.Disconnected += disconnect;
        var peer = await Connect(server, remote);
        var client = registered
            ? new Client(peer.Server, 1, Guid.NewGuid(), Guid.Empty)
            : node.ResolveSender(peer.Server)!;
        if (registered) { node.AddClient(client); }

        var notifications = 0;
        node.OnClientStateChanged += changed =>
        {
            notifications++;
            node.QueuePackage(ClientPackageBuilder.State(changed), PackageAudience.Except(changed));
        };
        var disconnected = false;
        server.Disconnected += (_, _) => disconnected = true;
        try
        {
            peer.Remote.Disconnect();
            await Pump(server, remote, () => disconnected);
            Assert.Equal(registered ? 1 : 0, notifications);
            Assert.Empty(node.Clients);
            Assert.Empty((Dictionary<Guid, Client>)GetField(node, "_transportClients")!);
        }
        finally { node.StopImmediate(); }
    }

    [Fact]
    public async Task LateOldPeerDisconnectCannotRemoveReplacementAtSameRoutingIdAndEndpoint()
    {
        await using var server = new NetworkHost();
        await using var remote = new NetworkHost();
        var node = new NetNode(NetNodeOptions.Default, false, PackageHandlerCatalog.Empty, NodeRole.Authority);
        var disconnect = DisconnectHandler(node);
        server.Disconnected += disconnect;
        NetworkDisconnectInfo? oldInfo = null;
        server.Disconnected += (_, info) => oldInfo = info;
        var first = await Connect(server, remote);
        var old = new Client(first.Server, 1, Guid.NewGuid(), Guid.Empty);
        node.AddClient(old);
        first.Remote.Disconnect();
        await Pump(server, remote, () => oldInfo != null);
        var second = await Connect(server, remote);
        var replacement = new Client(second.Server, 1, Guid.NewGuid(), Guid.Empty);
        node.AddClient(replacement);
        var epoch = replacement.ConnectionEpoch;
        var generation = replacement.ConnectionGeneration;
        Assert.Equal(first.Server.TransportEndpoint, second.Server.TransportEndpoint);
        Assert.NotEqual(first.Server.Id, second.Server.Id);
        Assert.True(generation > old.ConnectionGeneration);
        var notifications = 0;
        node.OnClientStateChanged += changed =>
        {
            notifications++;
            node.QueuePackage(ClientPackageBuilder.State(changed), PackageAudience.Except(changed));
        };
        try
        {
            // A late datagram can recreate an unregistered transport record after logical removal.
            node.ResolveSender(first.Server);
            disconnect(first.Server, oldInfo!);
            Assert.Same(replacement, node.Clients[1]);
            Assert.Equal(epoch, replacement.ConnectionEpoch);
            Assert.Equal(generation, replacement.ConnectionGeneration);
            Assert.Equal(0, notifications);
        }
        finally { node.StopImmediate(); }
    }

    [Fact]
    public async Task StopImmediateCancelsAnActualSecureHandshakeAndReclaimsTheHostPromptly()
    {
        using var blackhole = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        await using var host = new NetworkHost();
        var node = new NetNode(NetNodeOptions.Default, false, PackageHandlerCatalog.Empty, NodeRole.Client);
        using var cancellation = new CancellationTokenSource();
        var connecting = host.ConnectAsync(
            (IPEndPoint)blackhole.Client.LocalEndPoint!,
            ReadOnlyMemory<byte>.Empty,
            cancellation.Token
        );
        SetField(node, "_host", host);
        SetField(node, "_connecting", connecting);
        SetField(node, "_connectingCancellation", cancellation);
        await Task.Delay(100);
        var watch = Stopwatch.StartNew();
        node.StopImmediate();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => connecting.WaitAsync(TimeSpan.FromSeconds(3)));
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(3));
        Assert.Equal(NodeRole.Inactive, node.Role);
        Assert.Equal(0, host.SecureSessionCount);
        Assert.Equal(default, host.Resources);
    }

    private static Action<NetworkConnection, NetworkDisconnectInfo> DisconnectHandler(NetNode node)
    {
        return typeof(NetNode).GetMethod("PeerDisconnectedEvent", BindingFlags.NonPublic | BindingFlags.Instance)!
            .CreateDelegate<Action<NetworkConnection, NetworkDisconnectInfo>>(node);
    }

    private static async Task<(NetworkConnection Server, NetworkConnection Remote)> Connect(
        NetworkHost server,
        NetworkHost remote
    )
    {
        NetworkConnection? accepted = null;
        void OnConnected(NetworkConnection connection) => accepted = connection;
        void Accept(NetworkConnectionRequest request) => request.Accept();
        server.Connected += OnConnected;
        server.ConnectionRequested += Accept;
        try
        {
            var connecting = remote.ConnectAsync(
                new IPEndPoint(IPAddress.Loopback, server.LocalPort),
                ReadOnlyMemory<byte>.Empty,
                CancellationToken.None
            );
            await Pump(server, remote, () => connecting.IsCompleted && accepted != null);
            return (accepted!, await connecting);
        }
        finally
        {
            server.Connected -= OnConnected;
            server.ConnectionRequested -= Accept;
        }
    }

    private static async Task Pump(NetworkHost server, NetworkHost remote, Func<bool> complete)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (!complete() && DateTime.UtcNow < deadline)
        {
            server.DispatchEvents();
            remote.DispatchEvents();
            await Task.Delay(5);
        }

        Assert.True(complete(), "Real transport event did not arrive before the bounded deadline.");
    }

    private static void SetField(NetNode node, string name, object value)
    {
        typeof(NetNode).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(node, value);
    }

    private static object? GetField(NetNode node, string name)
    {
        return typeof(NetNode).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(node);
    }
}
