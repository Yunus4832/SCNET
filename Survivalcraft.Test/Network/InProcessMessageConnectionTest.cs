using Engine.Core.Mathematics;

using EntitySystem.Core;

using Game.Network;
using Game.Network.Handling;
using Game.Network.Transport;
using Game.Terrains.Distribution;

namespace Survivalcraft.Test.Network;

public sealed class InProcessMessageConnectionTest
{
    [Fact]
    public void AuthorityRemovalStopsTheLocalClientAndPreservesTheDisconnectReason()
    {
        var authority = new NetNode(
            NetNodeOptions.Default,
            true,
            PackageHandlerCatalog.Empty,
            NodeRole.Authority
        );
        var client = new NetNode(
            NetNodeOptions.Default,
            true,
            PackageHandlerCatalog.Empty,
            NodeRole.Client
        );
        var player = new Client(null, 1, Guid.NewGuid(), Guid.Empty)
        {
            State = ClientState.Playing, ConnectionPhase = ConnectionPhase.Live
        };
        var server = new Client(null, 0, Guid.NewGuid(), Guid.Empty);
        var self = new Client(null, player.ID, player.TokenId, Guid.Empty)
        {
            State = ClientState.Playing, ConnectionPhase = ConnectionPhase.Live
        };
        authority.AddClient(player);
        client.PrepareInProcessClient(server, self);
        client.CurrentStage = NetNode.Stage.Connected;
        var pair = InProcessMessageConnection.CreatePair(authority, player, client, server);
        using var toAuthority = pair.ToAuthority;
        using var toClient = pair.ToClient;
        var runtime = client.Runtime;
        string? reason = null;
        client.Stopped += (_, stopped, _, message) =>
        {
            Assert.Same(runtime, stopped);
            reason = message.Message.Fallback;
        };
        Assert.DoesNotContain(server, client.Clients.Values);
        Assert.Null(self.Connection);

        authority.RemoveClientImmediate(player, "Kicked by operator.");
        client.QueuePackage(new SubsystemTimePackage(1, 0), PackageAudience.To(server));
        client.Update();

        Assert.True(runtime.IsClosed);
        Assert.Equal(NetNode.Stage.NotConnected, client.CurrentStage);
        Assert.Equal("Kicked by operator.", reason);
        Assert.Equal(0, client.PendingPackageCount);
        authority.StopImmediate();
    }

    [Fact]
    public void LocalSnapshotsUseTheSharedInboxBudgetAndReleaseReservationsWhenTheLinkCloses()
    {
        var authority = new NetNode(
            NetNodeOptions.Default,
            true,
            PackageHandlerCatalog.Empty,
            NodeRole.Authority
        );
        var client = new NetNode(
            NetNodeOptions.Default,
            true,
            PackageHandlerCatalog.Empty,
            NodeRole.Client
        );
        var player = new Client(null, 1, Guid.NewGuid(), Guid.Empty);
        var server = new Client(null, 0, Guid.NewGuid(), Guid.Empty);
        client.Server = server;
        var content = new ClientChunkContentTransport(client.Runtime);
        var pair = InProcessMessageConnection.CreatePair(authority, player, client, server);
        using var toClient = pair.ToClient;
        using var toAuthority = pair.ToAuthority;
        var capacity = (int)(ClientChunkContentTransport.MaximumQueuedSnapshotBytes /
                             ClientChunkContentTransport.SnapshotBytes);
        var allocations = Enumerable.Range(0, capacity + 1)
            .Select(x => new ChunkAllocationId(new Point2(x, 0), 1)).ToArray();
        content.Request(allocations.Select(allocation => new ChunkContentRequest(allocation)).ToArray());
        var cells = new int[AuthorityChunkSnapshot.CellCount];
        var shafts = new long[AuthorityChunkSnapshot.ShaftCount];
        foreach (var allocation in allocations.Take(capacity))
        {
            content.Receive(new ClientChunkSnapshot(allocation, 1, cells, shafts));
        }

        var snapshot = new TerrainSnapshotMessage(new ClientChunkSnapshot(allocations[^1], 1, cells, shafts));
        var outbound = new OutboundPackage(snapshot, PackageAudience.To(player), authority.Runtime);
        Assert.Equal(ConnectionSendResult.Backpressured, toClient.TrySendOwned(outbound));
        Assert.Equal(0, WorldNetworkBinding.For(client.Runtime).Inbox.Count);
        content.DrainReceived(new List<ClientChunkSnapshot>());
        Assert.Equal(ConnectionSendResult.Accepted, toClient.TrySendOwned(outbound));
        Assert.Equal(ClientChunkContentTransport.SnapshotBytes, content.BufferedSnapshotBytes);
        Assert.Equal(0, content.PendingSnapshotBytes);
        toClient.Dispose();
        Assert.Equal(
            0,
            WorldNetworkBinding.For(client.Runtime).Inbox.Drain(
                1,
                _ => throw new InvalidOperationException("Closed delivery.")
            )
        );
        Assert.Equal(0, content.BufferedSnapshotBytes);
        authority.Runtime.Close();
        client.Runtime.Close();
    }

    [Fact]
    public void AReleasedWorldCannotQueueMessagesIntoTheNodesNextRuntime()
    {
        using var project = new Project();
        var node = new NetNode(
            NetNodeOptions.Default,
            true,
            PackageHandlerCatalog.Empty,
            NodeRole.Authority
        );
        var source = node.Runtime;
        source.BindProject(project);
        node.ReleaseProject(project);

        NetworkSender.Send(project, new SubsystemTimePackage(3, 0), PackageAudience.Global);

        Assert.True(source.IsClosed);
        Assert.NotSame(source, node.Runtime);
        Assert.Equal(0, node.PendingPackageCount);
    }

    [Fact]
    public void ClosingTheSourceRuntimeCancelsMessagesAlreadyQueuedAtTheOtherSide()
    {
        using var authorityProject = new Project();
        using var clientProject = new Project();
        var authority = new NetNode(
            NetNodeOptions.Default,
            true,
            PackageHandlerCatalog.Empty,
            NodeRole.Authority
        );
        var client = new NetNode(
            NetNodeOptions.Default,
            true,
            PackageHandlerCatalog.Empty,
            NodeRole.Client
        );
        authority.Runtime.BindProject(authorityProject);
        client.Runtime.BindProject(clientProject);
        var pair = InProcessMessageConnection.CreatePair(
            authority,
            new Client(Guid.Empty),
            client,
            new Client(Guid.Empty)
        );
        using var toAuthority = pair.ToAuthority;
        using var toClient = pair.ToClient;
        var deliveries = 0;
        client.OnReceive += (_, messages) => deliveries += messages.Count();
        Assert.Equal(
            ConnectionSendResult.Accepted,
            toClient.TrySendOwned(
                new OutboundPackage(new SubsystemTimePackage(1, 0), PackageAudience.Global, authority.Runtime)
            )
        );

        authority.Runtime.Close();

        Assert.Equal(0, client.PumpReceivedPackages());
        Assert.Equal(0, deliveries);
        Assert.False(toClient.IsConnected);
        Assert.False(toAuthority.IsConnected);
    }

    [Fact]
    public void OrdinaryNodeSendingRoutesThroughInProcessSessionsWithoutPeers()
    {
        using var authorityProject = new Project();
        using var clientProject = new Project();
        var authority = new NetNode(
            NetNodeOptions.Default,
            true,
            PackageHandlerCatalog.Empty,
            NodeRole.Authority
        );
        var client = new NetNode(
            NetNodeOptions.Default,
            true,
            PackageHandlerCatalog.Empty,
            NodeRole.Client
        );
        authority.Runtime.BindProject(authorityProject);
        client.Runtime.BindProject(clientProject);
        var playerSession = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid())
        {
            State = ClientState.Playing, ConnectionPhase = ConnectionPhase.Live
        };
        var authoritySession = new Client(Guid.Empty);
        authority.AddClient(playerSession);
        client.AddClient(authoritySession);
        client.Server = authoritySession;
        var pair = InProcessMessageConnection.CreatePair(authority, playerSession, client, authoritySession);
        using var toAuthority = pair.ToAuthority;
        using var toClient = pair.ToClient;
        var requests = new List<ReceivedPackage>();
        var responses = new List<ReceivedPackage>();
        authority.OnReceive += (_, messages) => requests.AddRange(messages);
        client.OnReceive += (_, messages) => responses.AddRange(messages);
        Assert.Null(playerSession.Peer);
        Assert.Null(authoritySession.Peer);
        Assert.True(playerSession.IsConnected);
        Assert.True(authoritySession.IsConnected);

        NetworkSender.SendToServer(
            client.Runtime,
            new TerrainChunksRequestPackage
                { ChunkRequests = [new ChunkContentRequest(new ChunkAllocationId(new Point2(0, 0), 1))] }
        );
        NetworkSender.Send(authorityProject, new SubsystemTimePackage(3, 0), PackageAudience.To(playerSession));
        Assert.Empty(requests);
        Assert.Empty(responses);
        client.Update();
        authority.Update();
        client.Update();

        Assert.IsType<TerrainChunksRequestPackage>(Assert.Single(requests).Package);
        Assert.Same(authorityProject, requests[0].Context.Project);
        Assert.IsType<SubsystemTimePackage>(Assert.Single(responses).Package);
        Assert.Same(clientProject, responses[0].Context.Project);
        Assert.Equal(0, authority.PendingPackageCount);
        Assert.Equal(0, client.PendingPackageCount);
        Assert.Null(authority.LocalPort);
        Assert.Null(client.LocalPort);
    }

    [Fact]
    public void OrdinarySendingKeepsAudienceFilteringForAnInProcessRecipient()
    {
        using var project = new Project();
        var authority = new NetNode(
            NetNodeOptions.Default,
            true,
            PackageHandlerCatalog.Empty,
            NodeRole.Authority
        );
        var client = new NetNode(
            NetNodeOptions.Default,
            true,
            PackageHandlerCatalog.Empty,
            NodeRole.Client
        );
        var player = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid())
        {
            State = ClientState.Playing, ConnectionPhase = ConnectionPhase.Live
        };
        authority.AddClient(player);
        var pair = InProcessMessageConnection.CreatePair(authority, player, client, new Client(Guid.Empty));
        using var toAuthority = pair.ToAuthority;
        using var toClient = pair.ToClient;
        var deliveries = 0;
        client.OnReceive += (_, messages) => deliveries += messages.Count();

        authority.QueuePackage(new SubsystemTimePackage(1, 0), PackageAudience.Except(player));
        authority.Update();
        client.Update();

        Assert.Equal(0, deliveries);
        var fanout = Assert.Single(authority.SendStatistics.GetFanout());
        Assert.Equal(1, fanout.Messages);
        Assert.Equal(0, fanout.RecipientDeliveries);
    }

    [Fact]
    public void BothDirectionsDeliverToTheirOwnWorldAndBoundSessionWithoutNetworkPorts()
    {
        using var authorityProject = new Project();
        using var clientProject = new Project();
        var authority = new NetNode(
            NetNodeOptions.Default,
            true,
            PackageHandlerCatalog.Empty,
            NodeRole.Authority
        );
        var client = new NetNode(
            NetNodeOptions.Default,
            true,
            PackageHandlerCatalog.Empty,
            NodeRole.Client
        );
        authority.Runtime.BindProject(authorityProject);
        client.Runtime.BindProject(clientProject);
        var playerSession = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid())
        {
            State = ClientState.Playing,
            ConnectionPhase = ConnectionPhase.Live
        };
        var authoritySession = new Client(Guid.Empty);
        authority.AddClient(playerSession);
        var requests = new List<ReceivedPackage>();
        var responses = new List<ReceivedPackage>();
        authority.OnReceive += (_, messages) => requests.AddRange(messages);
        client.OnReceive += (_, messages) => responses.AddRange(messages);
        var pair = InProcessMessageConnection.CreatePair(authority, playerSession, client, authoritySession);
        using var toAuthority = pair.ToAuthority;
        using var toClient = pair.ToClient;
        var request = new TerrainChunksRequestPackage
        {
            ChunkRequests = [new ChunkContentRequest(new ChunkAllocationId(new Point2(1, 2), 1))]
        };
        var response = new SubsystemTimePackage(17, 0);

        Assert.Equal(
            ConnectionSendResult.Accepted,
            toAuthority.TrySendOwned(new OutboundPackage(request, PackageAudience.Global, client.Runtime))
        );
        Assert.Equal(
            ConnectionSendResult.Accepted,
            toClient.TrySendOwned(new OutboundPackage(response, PackageAudience.Global, authority.Runtime))
        );
        Assert.Empty(requests);
        Assert.Empty(responses);
        Assert.Equal(1, authority.PumpReceivedPackages());
        Assert.Equal(1, client.PumpReceivedPackages());
        var deliveredRequest = Assert.IsType<TerrainChunksRequestPackage>(Assert.Single(requests).Package);
        Assert.NotSame(request, deliveredRequest);
        Assert.Equal(request.ChunkRequests, deliveredRequest.ChunkRequests);
        Assert.Same(authorityProject, requests[0].Context.Project);
        Assert.Same(playerSession, requests[0].Context.Sender);
        Assert.Same(response, Assert.Single(responses).Package);
        Assert.Same(clientProject, responses[0].Context.Project);
        Assert.Same(authoritySession, responses[0].Context.Sender);
        Assert.Null(authority.LocalPort);
        Assert.Null(client.LocalPort);
    }

    [Fact]
    public void DisconnectingEitherDirectionCancelsQueuedMessagesForBothDirections()
    {
        using var authorityProject = new Project();
        using var clientProject = new Project();
        var authority = new NetNode(
            NetNodeOptions.Default,
            true,
            PackageHandlerCatalog.Empty,
            NodeRole.Authority
        );
        var client = new NetNode(
            NetNodeOptions.Default,
            true,
            PackageHandlerCatalog.Empty,
            NodeRole.Client
        );
        var pair = InProcessMessageConnection.CreatePair(
            authority,
            new Client(Guid.Empty),
            client,
            new Client(Guid.Empty)
        );
        var delivered = 0;
        client.OnReceive += (_, _) => delivered++;
        Assert.Equal(
            ConnectionSendResult.Accepted,
            pair.ToClient.TrySendOwned(
                new OutboundPackage(new SubsystemTimePackage(1, 0), PackageAudience.Global, authority.Runtime)
            )
        );

        pair.ToAuthority.Dispose();

        Assert.False(pair.ToAuthority.IsConnected);
        Assert.False(pair.ToClient.IsConnected);
        Assert.Equal(
            ConnectionSendResult.Closed,
            pair.ToClient.TrySendOwned(
                new OutboundPackage(new SubsystemTimePackage(2, 0), PackageAudience.Global, authority.Runtime)
            )
        );
        Assert.Equal(0, client.PumpReceivedPackages());
        Assert.Equal(0, delivered);
        pair.ToClient.Dispose();
    }

    [Fact]
    public void AConnectionDoesNotRetargetANewRuntimeAfterTheWorldIsReleased()
    {
        using var authorityProject = new Project();
        using var clientProject = new Project();
        var authority = new NetNode(
            NetNodeOptions.Default,
            true,
            PackageHandlerCatalog.Empty,
            NodeRole.Authority
        );
        var client = new NetNode(
            NetNodeOptions.Default,
            true,
            PackageHandlerCatalog.Empty,
            NodeRole.Client
        );
        authority.Runtime.BindProject(authorityProject);
        client.Runtime.BindProject(clientProject);
        var pair = InProcessMessageConnection.CreatePair(
            authority,
            new Client(Guid.Empty),
            client,
            new Client(Guid.Empty)
        );
        using var toAuthority = pair.ToAuthority;
        using var toClient = pair.ToClient;

        client.ReleaseProject(clientProject);

        Assert.False(toClient.IsConnected);
        Assert.Equal(
            ConnectionSendResult.Closed,
            toClient.TrySendOwned(
                new OutboundPackage(new SubsystemTimePackage(1, 0), PackageAudience.Global, authority.Runtime)
            )
        );
        Assert.Equal(0, WorldNetworkBinding.For(client.Runtime).Inbox.Count);
    }
}
