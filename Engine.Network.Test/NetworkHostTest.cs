using System.Net;
using System.Net.Sockets;

using LiteNetLib;

using Rendezvous.Protocol;

namespace Engine.Network.Test;

public sealed class NetworkHostTest
{
    [Fact]
    public async Task ConfiguredChannelCountSupportsAdditionalChannelsAndRejectsOverflow()
    {
        var options = new NetworkHostOptions { ChannelCount = 9 };
        await using var server = new NetworkHost(options: options);
        await using var client = new NetworkHost(options: options);
        server.ConnectionRequested += request => request.Accept();
        var echoed = false;
        server.Received += (connection, message) => connection.Send(message.Payload.Span, 8);
        client.Received += (_, message) => echoed = message.Payload.Span.SequenceEqual("channel-eight"u8);
        var connecting = client.ConnectAsync(new IPEndPoint(IPAddress.Loopback, server.LocalPort),
            ReadOnlyMemory<byte>.Empty, CancellationToken.None);
        await PumpUntilAsync(server, client, () => connecting.IsCompleted);
        var connection = await connecting;
        connection.Send("channel-eight"u8, 8);
        await PumpUntilAsync(server, client, () => echoed);
        Assert.Throws<ArgumentOutOfRangeException>(() => connection.Send("overflow"u8, 9));
        Assert.Throws<ArgumentOutOfRangeException>(() => new NetworkHost(options: options with { ChannelCount = 0 }));
    }

    [Fact]
    public async Task ExplicitIPv4BindingLeavesOtherLocalAddressesAvailable()
    {
        var address = IPAddress.Parse("127.0.0.2");
        await using var server = new NetworkHost(options: new NetworkHostOptions
        {
            LocalIPv4Address = address,
            LocalIPv6Address = IPAddress.IPv6Loopback
        });
        using var otherAddress = new UdpClient(new IPEndPoint(IPAddress.Loopback, server.LocalPort));
        await using var client = new NetworkHost();
        server.ConnectionRequested += request => request.Accept();
        server.Received += (connection, message) => connection.Send(message.Payload.Span);
        var connecting = client.ConnectAsync(new IPEndPoint(address, server.LocalPort),
            ReadOnlyMemory<byte>.Empty, CancellationToken.None);
        await PumpUntilAsync(server, client, () => connecting.IsCompleted);
        var connection = await connecting;
        var echoed = false;
        client.Received += (_, message) => echoed = message.Payload.Span.SequenceEqual("bound-address"u8);
        connection.Send("bound-address"u8);
        await PumpUntilAsync(server, client, () => echoed);
        Assert.Equal(address, connection.TransportEndpoint.Address);
    }

    [Fact]
    public void LocalBindingRejectsAddressesFromTheWrongFamily()
    {
        Assert.Throws<ArgumentException>(() => new NetworkHost(options: new NetworkHostOptions
        {
            LocalIPv4Address = IPAddress.IPv6Loopback
        }));
        Assert.Throws<ArgumentException>(() => new NetworkHost(options: new NetworkHostOptions
        {
            LocalIPv6Address = IPAddress.Loopback
        }));
    }

    [Fact]
    public async Task RelayOfferCannotRemapAPendingHandshake()
    {
        await using var server = new NetworkHost();
        await using var client = new NetworkHost();
        NetworkConnectionRequest? pending = null;
        server.ConnectionRequested += request => pending = request;
        server.Received += (connection, message) => connection.Send(message.Payload.Span);
        var connecting = client.ConnectAsync(new IPEndPoint(IPAddress.Loopback, server.LocalPort),
            ReadOnlyMemory<byte>.Empty, CancellationToken.None);
        await PumpUntilAsync(server, client, () => pending is not null);
        var offer = new RelayAllocation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), pending!.TransportEndpoint.ToString(),
            new RelayRouteCredentials(Guid.NewGuid(), 0, new byte[32]), DateTimeOffset.UtcNow + TimeSpan.FromMinutes(1));
        Assert.Null(server.TryInstallRelay(offer));
        var accepted = pending.Accept();
        await PumpUntilAsync(server, client, () => connecting.IsCompleted);
        Assert.NotNull(accepted);
        Assert.Equal(NetworkPathKind.Direct, accepted.PathKind);
        var echoed = false;
        client.Received += (_, message) => echoed = message.Payload.Span.SequenceEqual("accepted-direct"u8);
        (await connecting).Send("accepted-direct"u8);
        await PumpUntilAsync(server, client, () => echoed);
        Assert.Null(server.Failure);
    }

    [Fact]
    public async Task RelayOfferCannotRemapAnExistingDirectConnection()
    {
        await using var server = new NetworkHost();
        await using var client = new NetworkHost();
        server.ConnectionRequested += request => request.Accept();
        server.Received += (connection, message) => connection.Send(message.Payload.Span);
        var endpoint = new IPEndPoint(IPAddress.Loopback, server.LocalPort);
        var connecting = client.ConnectAsync(endpoint, ReadOnlyMemory<byte>.Empty, CancellationToken.None);
        await PumpUntilAsync(server, client, () => connecting.IsCompleted);
        var connection = await connecting;
        var offer = new RelayAllocation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), endpoint.ToString(),
            new RelayRouteCredentials(Guid.NewGuid(), 1, new byte[32]), DateTimeOffset.UtcNow + TimeSpan.FromMinutes(1));
        Assert.Null(client.TryInstallRelay(offer));
        var echoed = false;
        client.Received += (_, message) => echoed = message.Payload.Span.SequenceEqual("direct-survives"u8);
        connection.Send("direct-survives"u8);
        await PumpUntilAsync(server, client, () => echoed);
        Assert.Equal(NetworkPathKind.Direct, connection.PathKind);
        Assert.Null(client.Failure);
    }

    [Fact]
    public async Task TransportDoesNotRequireTheSynchronizationContextThatCreatedHost()
    {
        var previous = SynchronizationContext.Current;
        NetworkHost server;
        NetworkHost client;
        try
        {
            SynchronizationContext.SetSynchronizationContext(new DormantSynchronizationContext());
            server = new NetworkHost();
            client = new NetworkHost();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
        await using var serverLifetime = server;
        await using var clientLifetime = client;
        server.ConnectionRequested += request => request.Accept();
        Task<NetworkConnection> connecting;
        try
        {
            SynchronizationContext.SetSynchronizationContext(new DormantSynchronizationContext());
            connecting = client.ConnectAsync(new IPEndPoint(IPAddress.Loopback, server.LocalPort),
                ReadOnlyMemory<byte>.Empty, CancellationToken.None);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
        await PumpUntilAsync(server, client, () => connecting.IsCompleted);
        var connection = await connecting;
        var receipt = connection.SendWithReceipt(new byte[64]);
        await receipt.Completion.WaitAsync(TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task AcceptedRequestAndConnectedNotificationShareConnectionIdentity()
    {
        await using var server = new NetworkHost();
        await using var client = new NetworkHost();
        NetworkConnection? accepted = null;
        NetworkConnection? notified = null;
        server.ConnectionRequested += request =>
        {
            accepted = request.Accept();
            Assert.Null(request.Accept());
        };
        server.Connected += connection => notified = connection;
        var connecting = client.ConnectAsync(new IPEndPoint(IPAddress.Loopback, server.LocalPort),
            ReadOnlyMemory<byte>.Empty, CancellationToken.None);
        await PumpUntilAsync(server, client, () => connecting.IsCompleted && notified is not null);
        Assert.Same(accepted, notified);
        Assert.True(accepted!.IsConnected);
    }

    [Fact]
    public async Task ReliableReceiveOverflowClosesConnectionWithReasonAndBoundsQueuedPayloads()
    {
        await using var server = new NetworkHost(options: new NetworkHostOptions
        {
            MaxDataNotifications = 1,
            MaxNotificationBytes = 1024,
            MaxControlNotifications = 16
        });
        await using var client = new NetworkHost();
        server.ConnectionRequested += request => request.Accept();
        var received = 0;
        server.Received += (_, _) => received++;
        NetworkDisconnectInfo? closed = null;
        client.Disconnected += (_, information) => closed = information;
        var connecting = client.ConnectAsync(new IPEndPoint(IPAddress.Loopback, server.LocalPort),
            ReadOnlyMemory<byte>.Empty, CancellationToken.None);
        await PumpUntilAsync(server, client, () => connecting.IsCompleted);
        var connection = await connecting;
        connection.Send(new byte[32]);
        connection.Send(new byte[32]);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (closed is null)
        {
            deadline.Token.ThrowIfCancellationRequested();
            client.DispatchEvents();
            await Task.Delay(5, deadline.Token);
        }
        Assert.Equal("receive-overload"u8.ToArray(), closed.Data.ToArray());
        Assert.True(server.PendingNotificationCount <= 17);
        Assert.Equal(0, received);
        server.DispatchEvents(100);
        Assert.Equal(1, received);
        Assert.Null(server.Failure);
    }

    [Fact]
    public async Task UnsupportedDeliveryClosesOnlyTheOffendingConnection()
    {
        await using var server = new NetworkHost();
        await using var client = new NetworkHost();
        server.ConnectionRequested += request => request.Accept();
        var received = false;
        server.Received += (_, _) => received = true;
        var listener = new EventBasedNetListener();
        var closed = false;
        listener.PeerConnectedEvent += peer => peer.Send(new byte[] { 1 }, DeliveryMethod.ReliableSequenced);
        listener.PeerDisconnectedEvent += (_, _) => closed = true;
        var offender = new NetManager(listener);
        Assert.True(offender.Start());
        try
        {
            offender.Connect(new IPEndPoint(IPAddress.Loopback, server.LocalPort), new LiteNetLib.Utils.NetDataWriter());
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while (!closed)
            {
                deadline.Token.ThrowIfCancellationRequested();
                server.DispatchEvents();
                offender.PollEvents();
                await Task.Delay(5, deadline.Token);
            }
            Assert.Null(server.Failure);
            var connecting = client.ConnectAsync(new IPEndPoint(IPAddress.Loopback, server.LocalPort),
                ReadOnlyMemory<byte>.Empty, CancellationToken.None);
            await PumpUntilAsync(server, client, () => connecting.IsCompleted);
            var connection = await connecting;
            connection.Send(new byte[] { 2 });
            await PumpUntilAsync(server, client, () => received);
            Assert.Null(server.Failure);
        }
        finally
        {
            offender.Stop();
        }
    }

    [Fact]
    public async Task RetainedRequestCannotBeAcceptedAfterItsDeadline()
    {
        await using var server = new NetworkHost(options: new NetworkHostOptions { RequestTimeout = TimeSpan.FromMilliseconds(200) });
        await using var client = new NetworkHost();
        NetworkConnectionRequest? request = null;
        server.ConnectionRequested += incoming => request = incoming;
        var connecting = client.ConnectAsync(new IPEndPoint(IPAddress.Loopback, server.LocalPort),
            ReadOnlyMemory<byte>.Empty, CancellationToken.None);
        await PumpUntilAsync(server, client, () => request is not null);
        await Assert.ThrowsAsync<NetworkConnectionException>(() => connecting);
        Assert.False(request!.IsPending);
        Assert.Null(request.Accept());
        Assert.False(request.Reject());
    }

    [Fact]
    public async Task UndispatchedHandshakeExpiresAndIsNotLaterOfferedToApplication()
    {
        await using var server = new NetworkHost(options: new NetworkHostOptions { RequestTimeout = TimeSpan.FromMilliseconds(100) });
        await using var client = new NetworkHost();
        var offered = false;
        server.ConnectionRequested += _ => offered = true;
        var connecting = client.ConnectAsync(new IPEndPoint(IPAddress.Loopback, server.LocalPort),
            ReadOnlyMemory<byte>.Empty, CancellationToken.None);
        var failure = await Assert.ThrowsAsync<NetworkConnectionException>(() => connecting);
        Assert.Equal(NetworkDisconnectReason.Rejected, failure.Information.Reason);
        Assert.Equal("request-expired"u8.ToArray(), failure.Information.Data.ToArray());
        server.DispatchEvents();
        Assert.False(offered);
    }

    [Fact]
    public async Task RemoteClosePreservesCopiedReasonAndReceiveDeliveryMode()
    {
        await using var server = new NetworkHost();
        await using var client = new NetworkHost();
        server.ConnectionRequested += request => request.Accept();
        NetworkMessage? message = null;
        NetworkDisconnectInfo? closed = null;
        server.Received += (_, received) => message = received;
        server.Disconnected += (_, information) => closed = information;
        var connecting = client.ConnectAsync(new IPEndPoint(IPAddress.Loopback, server.LocalPort),
            ReadOnlyMemory<byte>.Empty, CancellationToken.None);
        await PumpUntilAsync(server, client, () => connecting.IsCompleted);
        var connection = await connecting;
        connection.Send(new byte[] { 1 }, delivery: DeliveryMode.ReliableUnordered);
        await PumpUntilAsync(server, client, () => message is not null);
        Assert.Equal(DeliveryMode.ReliableUnordered, message!.Delivery);
        var reason = "world-closed"u8.ToArray();
        connection.Disconnect(reason);
        Array.Fill(reason, (byte)0);
        await PumpUntilAsync(server, client, () => closed is not null);
        Assert.Equal(NetworkDisconnectReason.RemoteClose, closed!.Reason);
        Assert.Equal("world-closed"u8.ToArray(), closed.Data.ToArray());
    }

    [Fact]
    public async Task FragmentedReliableReceiptCompletesWithoutDispatchingApplicationReceive()
    {
        await using var server = new NetworkHost();
        await using var client = new NetworkHost();
        server.ConnectionRequested += request => request.Accept();
        var received = false;
        server.Received += (_, _) => received = true;
        var connecting = client.ConnectAsync(new IPEndPoint(IPAddress.Loopback, server.LocalPort),
            ReadOnlyMemory<byte>.Empty, CancellationToken.None);
        await PumpUntilAsync(server, client, () => connecting.IsCompleted);
        var connection = await connecting;
        var receipt = connection.SendWithReceipt(new byte[4096]);
        await receipt.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(0, connection.PendingReliableBytes);
        Assert.False(received);
        server.DispatchEvents();
        Assert.True(received);
        Assert.Throws<ArgumentException>(() => connection.SendWithReceipt(new byte[] { 1 },
            delivery: DeliveryMode.Unreliable));
        Assert.Throws<ArgumentOutOfRangeException>(() => connection.Send(new byte[] { 1 }, channel: 8));
    }

    [Fact]
    public async Task ExpiredPreparationRejectsDatagramsAndHandshakeAndReleasesPath()
    {
        await using var host = new NetworkHost();
        var clock = new ManualClock();
        var released = false;
        var preparation = new ConnectionPreparation(host, new IPEndPoint(IPAddress.Loopback, 30000),
            NetworkPathKind.HolePunch, clock.GetUtcNow() + TimeSpan.FromMinutes(2), () => released = true, clock);
        clock.Now += TimeSpan.FromMinutes(2);
        Assert.Throws<TimeoutException>(() => preparation.SendDatagram(new byte[] { 1 }));
        await Assert.ThrowsAsync<TimeoutException>(() => preparation.ConnectAsync(ReadOnlyMemory<byte>.Empty,
            CancellationToken.None));
        await preparation.DisposeAsync();
        Assert.True(released);
    }

    [Fact]
    public async Task PreparationTransfersConnectionAndCannotBeConsumedTwice()
    {
        await using var server = new NetworkHost();
        await using var client = new NetworkHost();
        server.ConnectionRequested += request => request.Accept();
        var preparation = await client.PrepareAddressAsync("localhost", server.LocalPort, CancellationToken.None);
        server.DatagramReceived += (source, payload) => source.Send(payload);
        byte[]? datagram = null;
        preparation.DatagramReceived += payload => datagram = payload;
        preparation.SendDatagram("server-info-probe"u8);
        await PumpUntilAsync(server, client, () => datagram is not null);
        Assert.Equal("server-info-probe"u8.ToArray(), datagram);
        var connecting = preparation.ConnectAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None);
        await PumpUntilAsync(server, client, () => connecting.IsCompleted);
        var connection = await connecting;
        Assert.Equal(NetworkPathKind.Direct, connection.PathKind);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            preparation.ConnectAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None));
        await preparation.DisposeAsync();
        Assert.True(connection.IsConnected);
        var received = false;
        server.Received += (_, _) => received = true;
        connection.Send("after-transfer"u8);
        await PumpUntilAsync(server, client, () => received);
    }

    [Fact]
    public async Task DisposingPreparationCancelsItsHandshakeWithoutStoppingHost()
    {
        await using var server = new NetworkHost();
        await using var client = new NetworkHost();
        var preparation = await client.PrepareAddressAsync("127.0.0.1", server.LocalPort, CancellationToken.None);
        var connecting = preparation.ConnectAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None);
        await preparation.DisposeAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => connecting);
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            preparation.ConnectAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None));
        await using var replacement = await client.PrepareAddressAsync("127.0.0.1", server.LocalPort,
            CancellationToken.None);
        Assert.Equal(NetworkPathKind.Direct, replacement.PathKind);
    }

    [Fact]
    public async Task CandidateNotIntroducedByRegisteredBeaconCannotStartProbe()
    {
        await using var host = new NetworkHost();
        var candidate = new SignalMessage("candidate", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Endpoint: "127.0.0.1:30000", Challenge: new string('A', 64));
        var failure = await Assert.ThrowsAsync<RendezvousException>(() =>
            host.ProbeCandidateAsync(candidate, CancellationToken.None));
        Assert.Equal(RendezvousFailureKind.InvalidResponse, failure.Kind);
        Assert.Equal("unregistered_candidate", failure.ErrorCode);
    }

    [Fact]
    public async Task ReliableDataIsOrderedAndReconnectHasFreshIdentity()
    {
        await using var server = new NetworkHost();
        await using var client = new NetworkHost();
        server.ConnectionRequested += request => request.Accept();
        var received = new List<int>();
        server.Received += (_, payload) => received.Add(BitConverter.ToInt32(payload.Payload.Span));
        var connecting = client.ConnectAsync(new IPEndPoint(IPAddress.Loopback, server.LocalPort),
            ReadOnlyMemory<byte>.Empty, CancellationToken.None);
        await PumpUntilAsync(server, client, () => connecting.IsCompleted);
        var connection = await connecting;
        for (var index = 0; index < 32; index++)
        {
            connection.Send(BitConverter.GetBytes(index));
        }

        await PumpUntilAsync(server, client, () => received.Count == 32);
        Assert.Equal(Enumerable.Range(0, 32), received);
        var disconnected = false;
        client.Disconnected += (_, _) => disconnected = true;
        connection.Disconnect();
        await PumpUntilAsync(server, client, () => disconnected);
        var reconnecting = client.ConnectAsync(new IPEndPoint(IPAddress.Loopback, server.LocalPort),
            ReadOnlyMemory<byte>.Empty, CancellationToken.None);
        await PumpUntilAsync(server, client, () => reconnecting.IsCompleted);
        var replacement = await reconnecting;
        Assert.NotEqual(connection.Id, replacement.Id);
    }

    [Fact]
    public async Task RejectedHandshakeEndsAttempt()
    {
        await using var server = new NetworkHost();
        await using var client = new NetworkHost();
        server.ConnectionRequested += request => request.Reject("server-full"u8);
        var connecting = client.ConnectAsync(new IPEndPoint(IPAddress.Loopback, server.LocalPort),
            ReadOnlyMemory<byte>.Empty, CancellationToken.None);
        await PumpUntilAsync(server, client, () => connecting.IsCompleted);
        var failure = await Assert.ThrowsAsync<NetworkConnectionException>(() => connecting);
        Assert.Equal(NetworkDisconnectReason.Rejected, failure.Information.Reason);
        Assert.Equal("server-full"u8.ToArray(), failure.Information.Data.ToArray());
    }

    [Fact]
    public async Task IPv6ConnectionPreservesReliablePayloadAndReceipt()
    {
        await using var server = new NetworkHost();
        await using var client = new NetworkHost();
        server.ConnectionRequested += request => request.Accept();
        server.Received += (connection, message) => connection.Send(message.Payload.Span);
        var connecting = client.ConnectAsync(new IPEndPoint(IPAddress.IPv6Loopback, server.LocalPort),
            ReadOnlyMemory<byte>.Empty, CancellationToken.None);
        await PumpUntilAsync(server, client, () => connecting.IsCompleted);
        var connection = await connecting;
        var echoed = false;
        client.Received += (_, message) => echoed = message.Payload.Span.SequenceEqual("ipv6-payload"u8);

        var receipt = connection.SendWithReceipt("ipv6-payload"u8);
        await PumpUntilAsync(server, client, () => echoed && receipt.Completion.IsCompletedSuccessfully);

        Assert.Equal(IPAddress.IPv6Loopback, connection.TransportEndpoint.Address);
        Assert.Equal(0, connection.PendingReliableBytes);
        Assert.Null(server.Failure);
        Assert.Null(client.Failure);
    }

    [Fact]
    public async Task ClosingHostCompletesPendingOperationsAndClearsResources()
    {
        await using var server = new NetworkHost();
        await using var client = new NetworkHost();
        var endpoint = new IPEndPoint(IPAddress.Loopback, server.LocalPort);
        var connecting = client.ConnectAsync(endpoint, ReadOnlyMemory<byte>.Empty, CancellationToken.None);
        var signaling = client.RequestSignalAsync(endpoint, new SignalMessage("register", Guid.NewGuid()),
            CancellationToken.None);
        Assert.Equal(1, client.Resources.PendingConnections);
        Assert.Equal(1, client.Resources.PendingSignals);

        await client.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3));

        foreach (var operation in new Task[] { connecting, signaling })
        {
            var exception = await Record.ExceptionAsync(() => operation.WaitAsync(TimeSpan.FromSeconds(3)));
            Assert.True(exception is IOException or OperationCanceledException, exception?.ToString());
        }
        Assert.True(client.Completion.IsCompletedSuccessfully);
        Assert.Equal(default, client.Resources);
    }

    [Fact]
    public async Task DisposedHostRejectsNewOperationsAndCloseIsIdempotent()
    {
        var host = new NetworkHost();
        await host.DisposeAsync();
        await host.DisposeAsync();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => host.ConnectAsync(
            new IPEndPoint(IPAddress.Loopback, 1), ReadOnlyMemory<byte>.Empty, CancellationToken.None));
    }

    private static async Task PumpUntilAsync(NetworkHost server, NetworkHost client, Func<bool> completed)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        while (!completed())
        {
            deadline.Token.ThrowIfCancellationRequested();
            server.DispatchEvents();
            client.DispatchEvents();
            await Task.Delay(5, deadline.Token);
        }
    }

    private sealed class ManualClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UnixEpoch;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class DormantSynchronizationContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback callback, object? state)
        {
        }
    }
}
