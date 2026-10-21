using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;

using Engine.Network;

using Rendezvous.Protocol;

using RendezvousServer.Relay;
using RendezvousServer.Sessions;

namespace RendezvousServer.Test;

public sealed class UdpRelayRouteTest
{
    [Fact]
    public async Task ReusedEndpointWaitsForOldConnectionThenAcceptsFreshRouteIdentity()
    {
        await using var route = new UdpRelayRoute(new IPEndPoint(IPAddress.Loopback, 0));
        await using var server = new NetworkHost();
        await using var client = new NetworkHost();
        server.RegisterRelayRoute(route.Endpoint, route.GetCredentials(0));
        client.RegisterRelayRoute(route.Endpoint, route.GetCredentials(1));
        NetworkConnection? oldServerConnection = null;
        server.Connected += connection => oldServerConnection ??= connection;
        server.ConnectionRequested += request => request.Accept();
        server.Received += (connection, message) => connection.Send(message.Payload.Span);
        server.SendDatagram(route.Endpoint, "register"u8);
        client.SendDatagram(route.Endpoint, "register"u8);
        var connecting = client.ConnectAsync(route.Endpoint, ReadOnlyMemory<byte>.Empty, CancellationToken.None);
        await PumpUntilAsync([server, client], () => connecting.IsCompleted && oldServerConnection is not null);
        var oldConnection = await connecting;
        var fakeOffer = new RelayAllocation(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            route.Endpoint.ToString(),
            new RelayRouteCredentials(Guid.NewGuid(), 0, RandomNumberGenerator.GetBytes(32)),
            DateTimeOffset.UtcNow + TimeSpan.FromMinutes(1)
        );
        Assert.Null(server.TryInstallRelay(fakeOffer));
        var oldEcho = false;
        client.Received += (_, message) => oldEcho = message.Payload.Span.SequenceEqual("old-route-still-works"u8);
        oldConnection.Send("old-route-still-works"u8);
        await PumpUntilAsync([server, client], () => oldEcho);
        Assert.Null(server.Failure);
        oldConnection.Disconnect();
        await PumpUntilAsync([server, client], () => !oldServerConnection!.IsConnected);
        await route.DisposeAsync();

        await using var replacement = new UdpRelayRoute(route.Endpoint);
        var newOffer = fakeOffer with { Credentials = replacement.GetCredentials(0) };
        Assert.Equal(route.Endpoint, server.TryInstallRelay(newOffer));
        await using var newClient = new NetworkHost();
        newClient.RegisterRelayRoute(replacement.Endpoint, replacement.GetCredentials(1));
        newClient.SendDatagram(replacement.Endpoint, "register"u8);
        NetworkConnection? newServerConnection = null;
        server.Connected += connection => newServerConnection = connection;
        var newConnecting =
            newClient.ConnectAsync(replacement.Endpoint, ReadOnlyMemory<byte>.Empty, CancellationToken.None);
        await PumpUntilAsync(
            new[] { server, newClient },
            () => newConnecting.IsCompleted && newServerConnection is not null
        );
        var newEcho = false;
        newClient.Received += (_, message) => newEcho = message.Payload.Span.SequenceEqual("fresh-route"u8);
        (await newConnecting).Send("fresh-route"u8);
        await PumpUntilAsync(new[] { server, newClient }, () => newEcho);
        Assert.NotEqual(oldServerConnection!.Id, newServerConnection!.Id);
        Assert.Null(server.Failure);
    }

    [Fact]
    public async Task RoomCleanupPreservesConnectedRelayAndOnlyRemovesItsUnusedPreparations()
    {
        await using var active = new UdpRelayRoute(new IPEndPoint(IPAddress.Loopback, 0));
        await using var unused = new UdpRelayRoute(new IPEndPoint(IPAddress.Loopback, 0));
        await using var otherRoom = new UdpRelayRoute(new IPEndPoint(IPAddress.Loopback, 0));
        await using var server = new NetworkHost();
        await using var client = new NetworkHost();
        var session = Guid.NewGuid();
        var otherSession = Guid.NewGuid();

        RelayAllocation Offer(UdpRelayRoute route, Guid room) => new(
            room,
            Guid.NewGuid(),
            Guid.NewGuid(),
            route.Endpoint.ToString(),
            route.GetCredentials(0),
            DateTimeOffset.UtcNow + TimeSpan.FromMinutes(1)
        );

        Assert.NotNull(server.TryInstallRelay(Offer(active, session)));
        client.RegisterRelayRoute(active.Endpoint, active.GetCredentials(1));
        client.SendDatagram(active.Endpoint, "register"u8);
        server.ConnectionRequested += request => request.Accept();
        server.Received += (connection, message) => connection.Send(message.Payload.Span);
        var connecting = client.ConnectAsync(active.Endpoint, ReadOnlyMemory<byte>.Empty, CancellationToken.None);
        await PumpUntilAsync(new[] { server, client }, () => connecting.IsCompleted);
        Assert.NotNull(server.TryInstallRelay(Offer(unused, session)));
        Assert.NotNull(server.TryInstallRelay(Offer(otherRoom, otherSession)));
        Assert.Equal(3, server.Resources.RelayRoutes);
        server.ReleaseRoomPreparations(session);
        Assert.Equal(2, server.Resources.RelayRoutes);
        Assert.Equal(1, server.Resources.RelayPreparations);
        var echoed = false;
        client.Received += (_, message) => echoed = message.Payload.Span.SequenceEqual("room-stopped"u8);
        (await connecting).Send("room-stopped"u8);
        await PumpUntilAsync(new[] { server, client }, () => echoed);
        server.ReleaseRoomPreparations(otherSession);
        Assert.Equal(1, server.Resources.RelayRoutes);
        Assert.Equal(0, server.Resources.RelayPreparations);
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    public async Task HostShutdownNotifiesPeerBeforeClosingRelay(string address)
    {
        await using var route = new UdpRelayRoute(new IPEndPoint(IPAddress.Parse(address), 0));
        await using var server = new NetworkHost();
        await using var client = new NetworkHost();
        server.RegisterRelayRoute(route.Endpoint, route.GetCredentials(0));
        client.RegisterRelayRoute(route.Endpoint, route.GetCredentials(1));
        server.ConnectionRequested += request => request.Accept();
        server.Received += (connection, message) => connection.Send(message.Payload.Span);
        server.SendDatagram(route.Endpoint, "register"u8);
        client.SendDatagram(route.Endpoint, "register"u8);
        var connecting = client.ConnectAsync(route.Endpoint, ReadOnlyMemory<byte>.Empty, CancellationToken.None);
        await PumpUntilAsync(new[] { server, client }, () => connecting.IsCompleted);
        var echoed = false;
        client.Received += (_, message) => echoed = message.Payload.Span.SequenceEqual("relay-family"u8);
        var receipt = (await connecting).SendWithReceipt("relay-family"u8);
        await PumpUntilAsync(new[] { server, client }, () => echoed && receipt.Completion.IsCompletedSuccessfully);
        var closed =
            new TaskCompletionSource<NetworkDisconnectInfo>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.Disconnected += (_, information) => closed.TrySetResult(information);
        Assert.Equal(1, server.Resources.RelayRoutes);
        await server.DisposeAsync();
        Assert.Equal(default, server.Resources);
        await route.Completion.WaitAsync(TimeSpan.FromSeconds(2));
        await PumpUntilAsync(new[] { client }, () => closed.Task.IsCompleted);
        Assert.Equal(NetworkDisconnectReason.RemoteClose, (await closed.Task).Reason);
        Assert.Equal(0, client.Resources.RelayRoutes);
    }

    [Fact]
    public async Task ClosingInvitationPreservesAnEstablishedRelayConnection()
    {
        var invitations = new InvitationRegistry(TimeProvider.System, TimeSpan.FromMinutes(5), 1);
        var invitation = invitations.Create(1);
        var participant = invitations.Join(invitation.JoinCode, Guid.NewGuid());
        await using var routes = new RelayRouteRegistry(
            invitations,
            TimeProvider.System,
            IPAddress.Loopback,
            IPAddress.Loopback,
            0,
            1
        );
        var allocated = routes.Allocate(
            invitation.SessionId,
            participant.ParticipantId,
            Guid.NewGuid(),
            participant.Credential
        );
        var offer = Assert.Single(routes.GetHostOffers(invitation.SessionId, invitation.HostCredential));
        var endpoint = IPEndPoint.Parse(allocated.Endpoint);
        await using var server = new NetworkHost();
        await using var client = new NetworkHost();
        server.RegisterRelayRoute(endpoint, offer.Credentials);
        client.RegisterRelayRoute(endpoint, allocated.Credentials);
        server.ConnectionRequested += request => request.Accept();
        server.Received += (connection, message) => connection.Send(message.Payload.Span);
        server.SendDatagram(endpoint, "register"u8);
        client.SendDatagram(endpoint, "register"u8);
        var connecting = client.ConnectAsync(endpoint, ReadOnlyMemory<byte>.Empty, CancellationToken.None);
        await PumpUntilAsync(new[] { server, client }, () => connecting.IsCompleted);
        var connection = await connecting;
        invitations.Close(invitation.SessionId, invitation.HostCredential);
        Assert.Throws<InvalidOperationException>(() => invitations.Join(invitation.JoinCode, Guid.NewGuid()));
        var echoed = false;
        client.Received += (_, message) => echoed = message.Payload.Span.SequenceEqual("after-invitation-close"u8);
        connection.Send("after-invitation-close"u8);
        await PumpUntilAsync(new[] { server, client }, () => echoed);
        Assert.True(connection.IsConnected);
    }

    [Fact]
    public async Task AuthenticatedCloseStopsOnlyItsRouteAndReleasesTheSocket()
    {
        await using var route = new UdpRelayRoute(new IPEndPoint(IPAddress.Loopback, 0));
        await using var other = new UdpRelayRoute(new IPEndPoint(IPAddress.Loopback, 0));
        using var sender = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var credentials = route.GetCredentials(1);
        var control = SignalCodec.Encode(new SignalMessage("relay-close", Guid.NewGuid()));
        var payload = new byte[control.Length + 1];
        control.CopyTo(payload, 1);
        var invalid = RelayDatagramCodec.Encode(
            credentials.RouteId,
            credentials.Side,
            1,
            RandomNumberGenerator.GetBytes(32),
            payload
        );
        await sender.SendAsync(invalid, route.Endpoint);
        var valid = RelayDatagramCodec.Encode(credentials.RouteId, credentials.Side, 1, credentials.Key, payload);
        await sender.SendAsync(valid, route.Endpoint);
        await route.Completion.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(route.Completion.IsCompletedSuccessfully);
        Assert.False(other.Completion.IsCompleted);
        using var rebound = new UdpClient(route.Endpoint);
    }

    [Fact]
    public async Task DisconnectNotifiesRelayWithoutWaitingForItsIdleTimeout()
    {
        await using var route = new UdpRelayRoute(new IPEndPoint(IPAddress.Loopback, 0));
        await using var server = new NetworkHost();
        await using var client = new NetworkHost();
        server.RegisterRelayRoute(route.Endpoint, route.GetCredentials(0));
        client.RegisterRelayRoute(route.Endpoint, route.GetCredentials(1));
        server.ConnectionRequested += request => request.Accept();
        server.SendDatagram(route.Endpoint, "register"u8);
        client.SendDatagram(route.Endpoint, "register"u8);
        var connecting = client.ConnectAsync(route.Endpoint, ReadOnlyMemory<byte>.Empty, CancellationToken.None);
        await PumpUntilAsync(new[] { server, client }, () => connecting.IsCompleted);
        (await connecting).Disconnect();
        await route.Completion.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Null(server.Failure);
        Assert.Null(client.Failure);
    }

    [Fact]
    public async Task UnreachableRelayAppliesReliableSendBudgetAndCloseReleasesIt()
    {
        await using var route = new UdpRelayRoute(new IPEndPoint(IPAddress.Loopback, 0));
        await using var server = new NetworkHost();
        await using var client = new NetworkHost(options: new NetworkHostOptions { MaxPendingReliableBytes = 8 });
        server.RegisterRelayRoute(route.Endpoint, route.GetCredentials(0));
        client.RegisterRelayRoute(route.Endpoint, route.GetCredentials(1));
        server.ConnectionRequested += request => request.Accept();
        server.SendDatagram(route.Endpoint, "register"u8);
        client.SendDatagram(route.Endpoint, "register"u8);
        var connecting = client.ConnectAsync(route.Endpoint, ReadOnlyMemory<byte>.Empty, CancellationToken.None);
        await PumpUntilAsync(new[] { server, client }, () => connecting.IsCompleted);
        var connection = await connecting;
        await route.DisposeAsync();
        Assert.Equal(NetworkSendResult.Accepted, connection.TrySend(new byte[8]));
        Assert.Equal(8, connection.PendingReliableBytes);
        Assert.Equal(NetworkSendResult.Backpressure, connection.TrySend(new byte[1]));
        Assert.Equal(NetworkSendResult.TooLarge, connection.TrySend(new byte[9]));
        var refused = Assert.Throws<NetworkSendException>(() => connection.Send(new byte[1]));
        Assert.Equal(NetworkSendResult.Backpressure, refused.Result);
        await client.DisposeAsync();
        Assert.Equal(0, connection.PendingReliableBytes);
        Assert.Equal(NetworkSendResult.Closed, connection.TrySend(new byte[1]));
    }

    [Fact]
    public async Task RawRelayRejectsForgedCredentialsAndReplayedDatagrams()
    {
        await using var route = new UdpRelayRoute(new IPEndPoint(IPAddress.Loopback, 0));
        using var first = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var second = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var firstCredentials = route.GetCredentials(0);
        var secondCredentials = route.GetCredentials(1);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await first.SendAsync(
            RelayDatagramCodec.Encode(route.Id, 0, 1, firstCredentials.Key, new byte[] { 1 }),
            route.Endpoint,
            deadline.Token
        );
        await second.SendAsync(
            RelayDatagramCodec.Encode(route.Id, 1, 1, secondCredentials.Key, new byte[] { 2 }),
            route.Endpoint,
            deadline.Token
        );
        var registration = await first.ReceiveAsync(deadline.Token);
        Assert.True(RelayDatagramCodec.TryDecode(registration.Buffer, firstCredentials.Key, out _));
        var valid = RelayDatagramCodec.Encode(route.Id, 0, 2, firstCredentials.Key, new byte[] { 3 });
        await first.SendAsync(valid, route.Endpoint, deadline.Token);
        var received = await second.ReceiveAsync(deadline.Token);
        Assert.True(RelayDatagramCodec.TryDecode(received.Buffer, secondCredentials.Key, out var decoded));
        Assert.Equal(new byte[] { 3 }, decoded!.Payload);
        await first.SendAsync(valid, route.Endpoint, deadline.Token);
        await first.SendAsync(
            RelayDatagramCodec.Encode(
                route.Id,
                0,
                3,
                RandomNumberGenerator.GetBytes(32),
                new byte[] { 4 }
            ),
            route.Endpoint,
            deadline.Token
        );
        using var absent = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await second.ReceiveAsync(absent.Token));
    }

    [Fact]
    public async Task TwoRelayClientsAndDirectClientShareHostWithoutMixingPayloads()
    {
        await using var server = new NetworkHost();
        await using var first = new NetworkHost();
        await using var second = new NetworkHost();
        await using var direct = new NetworkHost();
        await using var firstRoute = new UdpRelayRoute(new IPEndPoint(IPAddress.Loopback, 0));
        await using var secondRoute = new UdpRelayRoute(new IPEndPoint(IPAddress.Loopback, 0));
        server.RegisterRelayRoute(firstRoute.Endpoint, firstRoute.GetCredentials(0));
        first.RegisterRelayRoute(firstRoute.Endpoint, firstRoute.GetCredentials(1));
        server.RegisterRelayRoute(secondRoute.Endpoint, secondRoute.GetCredentials(0));
        second.RegisterRelayRoute(secondRoute.Endpoint, secondRoute.GetCredentials(1));
        server.ConnectionRequested += request => request.Accept();
        server.Received += (connection, payload) => connection.Send(payload.Payload.Span);
        server.SendDatagram(firstRoute.Endpoint, "register"u8);
        server.SendDatagram(secondRoute.Endpoint, "register"u8);
        first.SendDatagram(firstRoute.Endpoint, "register"u8);
        second.SendDatagram(secondRoute.Endpoint, "register"u8);
        var firstConnect = first.ConnectAsync(firstRoute.Endpoint, ReadOnlyMemory<byte>.Empty, CancellationToken.None);
        var secondConnect =
            second.ConnectAsync(secondRoute.Endpoint, ReadOnlyMemory<byte>.Empty, CancellationToken.None);
        var directConnect = direct.ConnectAsync(
            new IPEndPoint(IPAddress.Loopback, server.LocalPort),
            ReadOnlyMemory<byte>.Empty,
            CancellationToken.None
        );
        var hosts = new[] { server, first, second, direct };
        await PumpUntilAsync(
            hosts,
            () => firstConnect.IsCompleted && secondConnect.IsCompleted && directConnect.IsCompleted
        );
        var connections = await Task.WhenAll(firstConnect, secondConnect, directConnect);
        var echoed = new Dictionary<int, byte[]>();
        first.Received += (_, payload) => echoed[0] = payload.Payload.ToArray();
        second.Received += (_, payload) => echoed[1] = payload.Payload.ToArray();
        direct.Received += (_, payload) => echoed[2] = payload.Payload.ToArray();
        for (var index = 0; index < connections.Length; index++)
        {
            connections[index].Send(Enumerable.Repeat((byte)(index + 1), 4096).ToArray());
        }

        await PumpUntilAsync(hosts, () => echoed.Count == 3);
        for (var index = 0; index < connections.Length; index++)
        {
            Assert.Equal(Enumerable.Repeat((byte)(index + 1), 4096), echoed[index]);
        }

        Assert.True(firstRoute.ForwardedBytes > 0);
        Assert.True(secondRoute.ForwardedBytes > 0);
        await firstRoute.DisposeAsync();
        Assert.True(firstRoute.Completion.IsCompletedSuccessfully);
        var pendingReceipt = connections[0].SendWithReceipt("unreachable-relay"u8);
        await first.DisposeAsync();
        await Assert.ThrowsAsync<IOException>(() => pendingReceipt.Completion);
        var relaySurvived = false;
        var directSurvived = false;
        second.Received += (_, payload) =>
        {
            Assert.True(payload.Payload.Span.SequenceEqual("relay-survives-other-route-close"u8));
            relaySurvived = true;
        };
        direct.Received += (_, payload) =>
        {
            Assert.True(payload.Payload.Span.SequenceEqual("direct-survives-other-route-close"u8));
            directSurvived = true;
        };
        var relayReceipt = connections[1].SendWithReceipt("relay-survives-other-route-close"u8);
        var directReceipt = connections[2].SendWithReceipt("direct-survives-other-route-close"u8);
        await PumpUntilAsync(hosts, () => relaySurvived && directSurvived);
        await Task.WhenAll(relayReceipt.Completion, directReceipt.Completion).WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(0, connections[1].PendingReliableBytes);
        Assert.Equal(0, connections[2].PendingReliableBytes);
        Assert.True(connections[1].IsConnected);
        Assert.True(connections[2].IsConnected);
        Assert.Null(server.Failure);
    }

    private static async Task PumpUntilAsync(IEnumerable<NetworkHost> hosts, Func<bool> completed)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!completed())
        {
            deadline.Token.ThrowIfCancellationRequested();
            foreach (var host in hosts)
            {
                host.DispatchEvents();
            }

            await Task.Delay(5, deadline.Token);
        }
    }
}
