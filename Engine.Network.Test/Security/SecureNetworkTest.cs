using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;

using Engine.Network.Security;

namespace Engine.Network.Test.Security;

public sealed class SecureNetworkTest
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HandshakeAndAllDeliveryModesAreEncryptedWithMatchingAuthenticatedBindings(bool relay)
    {
        using var serverIdentity = NetworkIdentity.Create();
        using var clientIdentity = NetworkIdentity.Create();
        await using var server = new NetworkHost(
            options: new NetworkHostOptions
            {
                Identity = serverIdentity,
                ValidatePeerIdentity = identity => identity.Address == clientIdentity.PublicIdentity.Address
            }
        );
        await using var proxy = new DatagramSecurityProxy(new IPEndPoint(IPAddress.Loopback, server.LocalPort), relay);
        await using var client = new NetworkHost(
            options: new NetworkHostOptions
            {
                Identity = clientIdentity,
                ValidatePeerIdentity = identity => identity.Address == serverIdentity.PublicIdentity.Address
            }
        );
        if (relay) { proxy.InstallRoutes(client, server); }

        var handshake = "private-application-handshake-never-plaintext"u8.ToArray();
        NetworkConnection? accepted = null;
        server.ConnectionRequested += request =>
        {
            Assert.Equal(handshake, request.Handshake.ToArray());
            accepted = request.Accept();
        };
        var echo = new List<byte[]>();
        server.Received += (connection, message) => connection.Send(message.Payload.Span);
        client.Received += (_, message) => echo.Add(message.Payload.ToArray());
        var connecting = client.ConnectAsync(proxy.Endpoint, handshake, CancellationToken.None);
        await PumpAsync(server, client, () => connecting.IsCompleted);
        var connection = await connecting;
        Assert.NotNull(accepted);
        Assert.Equal(serverIdentity.PublicIdentity.Address, connection.PeerIdentity.Address);
        Assert.Equal(clientIdentity.PublicIdentity.Address, accepted.PeerIdentity.Address);
        Assert.Equal(connection.ChannelBinding.ToArray(), accepted.ChannelBinding.ToArray());
        Assert.Equal(32, connection.ChannelBinding.Length);
        foreach (var mode in Enum.GetValues<DeliveryMode>())
        {
            var count = echo.Count;
            connection.Send("secret-game-payload-cannot-be-captured"u8, 1, mode);
            await PumpAsync(server, client, () => echo.Count > count);
        }

        var large = RandomNumberGenerator.GetBytes(512 * 1024);
        connection.Send(large);
        await PumpAsync(server, client, () => echo.Any(p => p.Length == large.Length));
        Assert.Equal(large, echo.First(p => p.Length == large.Length));
        foreach (var packet in proxy.ClientPackets.Concat(proxy.ServerPackets))
        {
            Assert.False(packet.AsSpan().IndexOf(handshake) >= 0);
            Assert.False(packet.AsSpan().IndexOf("secret-game-payload-cannot-be-captured"u8) >= 0);
            Assert.True(packet.Length <= 1200, $"Wire datagram exceeded budget: {packet.Length}");
        }

        var maximum = connection.GetMaxPayloadSize(DeliveryMode.Unreliable);
        Assert.True(maximum < 461);
        Assert.Equal(
            NetworkSendResult.TooLarge,
            connection.TrySend(new byte[maximum + 1], delivery: DeliveryMode.Unreliable)
        );
        Assert.Equal(
            NetworkSendResult.Accepted,
            connection.TrySend(new byte[maximum], delivery: DeliveryMode.Unreliable)
        );
    }

    [Fact]
    public async Task CorruptedAndReplayedRecordsDoNotReachApplicationOrDestroyTheConnection()
    {
        await using var server = new NetworkHost();
        await using var client = new NetworkHost();
        await using var proxy = new DatagramSecurityProxy(new IPEndPoint(IPAddress.Loopback, server.LocalPort));
        server.ConnectionRequested += request => request.Accept();
        var received = 0;
        server.Received += (_, _) => received++;
        var connecting = client.ConnectAsync(proxy.Endpoint, ReadOnlyMemory<byte>.Empty, CancellationToken.None);
        await PumpAsync(server, client, () => connecting.IsCompleted);
        var connection = await connecting;
        connection.Send("original-message"u8, delivery: DeliveryMode.Unreliable);
        await PumpAsync(server, client, () => received == 1);
        var replay = proxy.ClientPackets.Last(p => p.Length > 50 && p[5] == 23);
        await proxy.ReplayAsync(replay);
        var corrupted = replay.ToArray();
        corrupted[^1] ^= 1;
        await proxy.ReplayAsync(corrupted);
        await DelayPumpAsync(server, client, 100);
        Assert.Equal(1, received);
        proxy.tamperNext = 1;
        // Keep this application record above the proxy's control-packet filter for either AEAD layout.
        connection.Send(new byte[128], delivery: DeliveryMode.Unreliable);
        await DelayPumpAsync(server, client, 100);
        Assert.Equal(1, received);
        connection.Send("connection-still-alive"u8);
        await PumpAsync(server, client, () => received == 2);
        Assert.Null(server.Failure);
    }

    [Fact]
    public async Task OversizedDtlsRecordCannotTearDownAnEstablishedConnection()
    {
        await using var server = new NetworkHost();
        await using var client = new NetworkHost();
        await using var proxy = new DatagramSecurityProxy(new IPEndPoint(IPAddress.Loopback, server.LocalPort));
        server.ConnectionRequested += request => request.Accept();
        var received = false;
        server.Received += (_, _) => received = true;
        var connecting = client.ConnectAsync(proxy.Endpoint, ReadOnlyMemory<byte>.Empty, CancellationToken.None);
        await PumpAsync(server, client, () => connecting.IsCompleted);
        var connection = await connecting;
        var forged = new byte[4096];
        forged[0] = 8;
        "SCDT"u8.CopyTo(forged.AsSpan(1));
        await proxy.ReplayAsync(forged);
        connection.Send("oversize-forgery-rejected"u8);
        await PumpAsync(server, client, () => received);
        Assert.Null(server.Failure);
    }

    [Fact]
    public async Task WrongPinnedKeyRejectsBeforeApplicationHandshake()
    {
        await using var server = new NetworkHost();
        using var wrong = NetworkIdentity.Create();
        await using var client = new NetworkHost(
            options: new NetworkHostOptions
            {
                ValidatePeerIdentity = identity => identity.Address == wrong.PublicIdentity.Address
            }
        );
        var requested = false;
        server.ConnectionRequested += _ => requested = true;
        var connecting = client.ConnectAsync(
            new IPEndPoint(IPAddress.Loopback, server.LocalPort),
            "must-not-be-sent"u8.ToArray(),
            CancellationToken.None
        );
        await PumpAsync(server, client, () => connecting.IsCompleted);
        await Assert.ThrowsAsync<IOException>(() => connecting);
        Assert.False(requested);
        Assert.Equal(0, client.SecureSessionCount);
    }

    [Fact]
    public async Task IdentityExportImportIsStableButReconnectionsUseFreshSessionKeys()
    {
        using var identity = NetworkIdentity.Create();
        var key = identity.ExportPrivateKey();
        using var restored = NetworkIdentity.Import(key);
        CryptographicOperations.ZeroMemory(key);
        Assert.Equal(identity.PublicIdentity.Address, restored.PublicIdentity.Address);
        await using var server = new NetworkHost(options: new NetworkHostOptions { Identity = identity });
        server.ConnectionRequested += request => request.Accept();
        byte[]? previous = null;
        for (var round = 0; round < 2; round++)
        {
            await using var client = new NetworkHost();
            var connecting = client.ConnectAsync(
                new IPEndPoint(IPAddress.Loopback, server.LocalPort),
                ReadOnlyMemory<byte>.Empty,
                CancellationToken.None
            );
            await PumpAsync(server, client, () => connecting.IsCompleted);
            var connection = await connecting;
            if (previous is not null) { Assert.NotEqual(previous, connection.ChannelBinding.ToArray()); }

            previous = connection.ChannelBinding.ToArray();
            Assert.Equal(identity.PublicIdentity.Address, connection.PeerIdentity.Address);
            connection.Disconnect();
            await PumpAsync(server, client, () => !connection.IsConnected);
        }
    }

    [Fact]
    public async Task PublicDiscoverySurvivesButPlaintextConnectionSpoofingIsNotAccepted()
    {
        await using var server = new NetworkHost();
        await using var client = new NetworkHost();
        var datagram = false;
        var requested = false;
        server.DatagramReceived += (_, packet) => datagram = packet.AsSpan().SequenceEqual("public-probe"u8);
        server.ConnectionRequested += _ => requested = true;
        client.SendDatagram(new IPEndPoint(IPAddress.Loopback, server.LocalPort), "public-probe"u8);
        await PumpAsync(server, client, () => datagram);
        using var attacker = new UdpClient();
        var forged = new byte[128];
        forged[0] = 5;
        await attacker.SendAsync(forged, new IPEndPoint(IPAddress.Loopback, server.LocalPort));
        await DelayPumpAsync(server, client, 100);
        Assert.False(requested);
        Assert.Equal(0, server.SecureSessionCount);
    }

    [Fact]
    public async Task LossAndDuplicationDuringHandshakeAndReliableTransferConverge()
    {
        await using var server = new NetworkHost();
        await using var client = new NetworkHost();
        await using var proxy = new DatagramSecurityProxy(new IPEndPoint(IPAddress.Loopback, server.LocalPort))
        {
            dropPercent = 8, duplicatePercent = 15
        };
        server.ConnectionRequested += request => request.Accept();
        var data = new List<byte[]>();
        server.Received += (_, message) => data.Add(message.Payload.ToArray());
        var connecting = client.ConnectAsync(proxy.Endpoint, ReadOnlyMemory<byte>.Empty, CancellationToken.None);
        await PumpAsync(server, client, () => connecting.IsCompleted, 12);
        var connection = await connecting;
        for (var i = 0; i < 100; i++) { connection.Send(BitConverter.GetBytes(i)); }

        await PumpAsync(server, client, () => data.Count == 100, 12);
        Assert.Equal(Enumerable.Range(0, 100), data.Select(bytes => BitConverter.ToInt32(bytes)));
    }

    [Fact]
    public async Task OutOfOrderUnreliableDatagramsRemainValidAndAreNotMistakenForReplay()
    {
        await using var server = new NetworkHost();
        await using var client = new NetworkHost();
        await using var proxy = new DatagramSecurityProxy(new IPEndPoint(IPAddress.Loopback, server.LocalPort));
        server.ConnectionRequested += request => request.Accept();
        var received = new List<byte>();
        server.Received += (_, message) => received.Add(message.Payload.Span[0]);
        var connecting = client.ConnectAsync(proxy.Endpoint, ReadOnlyMemory<byte>.Empty, CancellationToken.None);
        await PumpAsync(server, client, () => connecting.IsCompleted);
        var connection = await connecting;
        proxy.reorderNext = 1;
        var first = new byte[400];
        first[0] = 1;
        connection.Send(first, delivery: DeliveryMode.Unreliable);
        await PumpAsync(server, client, () => proxy.HasHeldRecord);
        var second = new byte[400];
        second[0] = 2;
        connection.Send(second, delivery: DeliveryMode.Unreliable);
        await PumpAsync(server, client, () => received.Count == 2);
        Assert.Equal(new byte[] { 2, 1 }, received);
    }

    [Fact]
    public async Task PendingHandshakeLimitCancellationAndHostCloseReleaseSecureResources()
    {
        using var blackhole = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var second = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        await using var host = new NetworkHost(options: new NetworkHostOptions { MaxPendingSecureHandshakes = 1 });
        using var cancellation = new CancellationTokenSource();
        var pending = host.ConnectAsync(
            (IPEndPoint)blackhole.Client.LocalEndPoint!,
            ReadOnlyMemory<byte>.Empty,
            cancellation.Token
        );
        Assert.Equal(1, host.SecureSessionCount);
        Assert.Equal(1, host.Resources.PendingConnections);
        await Assert.ThrowsAsync<IOException>(() => host.ConnectAsync(
                (IPEndPoint)second.Client.LocalEndPoint!,
                ReadOnlyMemory<byte>.Empty,
                CancellationToken.None
            )
        );
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(3)));
        Assert.Equal(0, host.SecureSessionCount);
        var next = host.ConnectAsync(
            (IPEndPoint)second.Client.LocalEndPoint!,
            ReadOnlyMemory<byte>.Empty,
            CancellationToken.None
        );
        await host.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => next.WaitAsync(TimeSpan.FromSeconds(3)));
        Assert.Equal(0, host.SecureSessionCount);
        Assert.Equal(default, host.Resources);
    }

    private static async Task DelayPumpAsync(NetworkHost server, NetworkHost client, int milliseconds)
    {
        var end = Environment.TickCount64 + milliseconds;
        await PumpAsync(server, client, () => Environment.TickCount64 >= end);
    }

    private static async Task PumpAsync(NetworkHost server, NetworkHost client, Func<bool> complete, int seconds = 8)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(seconds));
        while (!complete())
        {
            timeout.Token.ThrowIfCancellationRequested();
            server.DispatchEvents(4096);
            client.DispatchEvents(4096);
            await Task.Delay(1, timeout.Token);
        }

        server.DispatchEvents(4096);
        client.DispatchEvents(4096);
    }
}
