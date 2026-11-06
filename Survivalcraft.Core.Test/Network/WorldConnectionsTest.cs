using Engine.Core.Mathematics;
using Engine.Core.Security;

using EntitySystem.Core;

using Game.Network;
using Game.Network.Transport;
using Game.Protocol;
using Game.Protocol.Connection;
using Game.Protocol.Entities;
using Game.Protocol.Identity;
using Game.Protocol.Terrain;
using Game.World;

namespace Survivalcraft.Core.Test.Network;

public sealed class WorldConnectionsTest
{
    [Fact]
    public void AuthorityLinkIsNotInventedAsAPlayerRosterEntry()
    {
        var world = new WorldRuntimeContext(RuntimeSide.Client, false);
        using var connections = new WorldConnections(world);
        var authority = new WorldConnection();
        var self = new WorldConnection(1, Guid.NewGuid());
        connections.SetAuthority(authority);
        connections.SetSelf(self);
        connections.Register(self);

        Assert.Same(authority, connections.Authority);
        Assert.Same(self, connections.Members[1]);
        Assert.Single(connections.Members);
        Assert.Same(world, authority.World);
        world.Close();
    }

    [Fact]
    public void RoutingIdReuseRequiresRemovalAndPreservesAudienceInstanceIdentity()
    {
        var world = new WorldRuntimeContext(RuntimeSide.Authority, false);
        using var connections = new WorldConnections(world);
        var first = new WorldConnection(1, Guid.NewGuid());
        var replacement = new WorldConnection(1, first.CharacterId);
        connections.Register(first);
        var audience = PackageAudience.To(first);

        Assert.Throws<InvalidOperationException>(() => connections.Register(replacement));
        Assert.Throws<InvalidOperationException>(() => first.AssignRoutingId(2));
        Assert.True(connections.Remove(first));
        connections.Register(replacement);
        Assert.False(connections.Remove(first));
        Assert.False(audience.Includes(replacement));
        Assert.Same(replacement, connections.FindCharacter(first.CharacterId));
        world.Close();
    }

    [Fact]
    public void SameLogicalRecordCarriesStateAndActualTransportLiveness()
    {
        using var project = new Project();
        var world = new WorldRuntimeContext(RuntimeSide.Authority, false);
        using var connections = new WorldConnections(world);
        world.BindProject(project);
        var transport = new Liveness { IsConnected = true };
        var connection = new TransportConnection(transport);
        connection.SetPlayerIdentity(new PlayerAddress(IdentityAddress.Encode(new byte[32])));
        connections.Register(connection);
        connection.State = ClientState.Playing;
        connection.ConnectionPhase = ConnectionPhase.Live;
        connection.ConnectionEpoch = Guid.NewGuid();

        Assert.Same(connection, connections.Members[1]);
        Assert.Equal(ConnectionPhase.Live, connections.Members[1].ConnectionPhase);
        Assert.Equal(connection.ConnectionEpoch, connections.Members[1].ConnectionEpoch);
        Assert.True(connections.Members[1].IsConnected);
        Assert.True(WorldConnectionQuery.IsPlayerOnline(project, connection));
        connection.State = ClientState.NotConnected;
        Assert.False(WorldConnectionQuery.IsPlayerOnline(project, connection));
        connection.State = ClientState.Playing;
        transport.IsConnected = false;
        Assert.False(connections.Members[1].IsConnected);
        Assert.False(WorldConnectionQuery.IsPlayerOnline(project, connection));
        world.Close();
    }

    [Fact]
    public void ClosingOldEpochClearsFactsAndCannotRebindItsConnectionsIntoAnotherWorld()
    {
        var oldWorld = new WorldRuntimeContext(RuntimeSide.Authority, false);
        using var oldConnections = new WorldConnections(oldWorld);
        var connection = new WorldConnection(1);
        oldConnections.Register(connection);
        oldConnections.SetSelf(connection);
        oldWorld.Close();

        Assert.Empty(oldConnections.Members);
        Assert.Null(oldConnections.Self);
        var newWorld = new WorldRuntimeContext(RuntimeSide.Authority, false);
        using var next = new WorldConnections(newWorld);
        Assert.Throws<InvalidOperationException>(() => next.Register(connection));
        newWorld.Close();
    }

    [Fact]
    public void ClientSubmissionAndExplicitAudienceUseTheSameOutboundPort()
    {
        var world = new WorldRuntimeContext(RuntimeSide.Client, false);
        using var connections = new WorldConnections(world);
        var authority = new TransportConnection(new Liveness { IsConnected = true });
        connections.SetAuthority(authority);
        var sink = new RecordingSink();
        using var endpoint = new WorldMessageEndpoint(world, sink);
        var message = new WakeupPackage();

        NetworkSender.SendToServer(world, message);
        NetworkSender.SendTo(authority, message);
        Assert.Equal(2, sink.Messages.Count);
        Assert.All(
            sink.Messages,
            item =>
            {
                Assert.Same(message, item.Message);
                Assert.Same(authority, item.Audience.SingleRecipient);
            }
        );

        world.Close();
        NetworkSender.SendToServer(world, message);
        NetworkSender.SendTo(authority, message);
        Assert.Equal(2, sink.Messages.Count);
        Assert.Throws<InvalidOperationException>(() => WorldMessageEndpoint.For(world));
    }

    private sealed class Liveness : IConnectionLiveness
    {
        public bool IsConnected { get; set; }
    }

    private sealed class TransportConnection : WorldConnection
    {
        public TransportConnection(IConnectionLiveness transport) : base(1)
        {
            BindLiveness(transport);
        }
    }

    private sealed class RecordingSink : IWorldMessageSink
    {
        public List<(IProtocolMessage Message, PackageAudience Audience)> Messages { get; } = [];

        public void Send(IProtocolMessage message, PackageAudience audience)
        {
            Messages.Add((message, audience));
        }

        public void SendNearPoint(Vector3 position, float radius, IProtocolMessage message) =>
            throw new NotSupportedException();

        public void SendToChunkObservers(Point2 chunk, IProtocolMessage message) => throw new NotSupportedException();

        public void SendToObservers(Entity entity, IProtocolMessage message, WorldConnection? except) =>
            throw new NotSupportedException();

        public bool CanQueueTerrainSnapshot(WorldConnection recipient) => throw new NotSupportedException();

        public bool TryQueueOwnedTerrainSnapshot(WorldConnection recipient, TerrainSnapshotMessage message) =>
            throw new NotSupportedException();

        public void DiscardOlderChunkAllocations(WorldConnection recipient, ChunkAllocationId allocation) =>
            throw new NotSupportedException();

        public void TrimChunkDelivery(WorldConnection recipient, Vector2 center, float distance) =>
            throw new NotSupportedException();

        public void ReleaseChunkDelivery(WorldConnection recipient) => throw new NotSupportedException();

        public void InvalidateChunkDelivery(Point2 coords) => throw new NotSupportedException();

        public void DiscardQueuedChunkRequests(ChunkAllocationId allocation) => throw new NotSupportedException();
    }
}
