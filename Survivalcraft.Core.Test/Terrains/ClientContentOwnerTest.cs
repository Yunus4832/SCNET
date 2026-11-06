using Engine.Core.Mathematics;

using EntitySystem.Core;

using Game.Network;
using Game.Network.Transport;
using Game.Protocol;
using Game.Protocol.Terrain;
using Game.Terrains.Distribution;
using Game.World;

namespace Survivalcraft.Core.Test.Terrains;

public sealed class ClientContentOwnerTest
{
    [Fact]
    public void CloseImmediatelyUnbindsTheActualQueueAndLateOwnedLeaseReleasesExactlyOnce()
    {
        using var project = new Project();
        var world = new WorldRuntimeContext(RuntimeSide.Client, false);
        world.BindProject(project);
        using var connections = new WorldConnections(world);
        connections.SetAuthority(new WorldConnection());
        var sink = new ContentSink();
        using var endpoint = new WorldMessageEndpoint(world, sink);
        var queue = new ClientChunkContentTransport(world);
        var allocation = new ChunkAllocationId(new Point2(1, 2), 3);
        queue.Request([new ChunkContentRequest(allocation)]);
        var released = 0;
        var reservation = Assert.IsType<ClientSnapshotReservation>(queue.TryReserve(allocation, () => released++));
        var message = new TerrainSnapshotMessage(Snapshot(allocation), reservation);
        Assert.Same(queue, ClientChunkContentTransport.For(world));
        Assert.True(ClientChunkContentTransport.TryGet(world, out var actual));
        Assert.Same(queue, actual);
        Assert.Equal(ClientChunkContentTransport.SnapshotBytes, queue.BufferedSnapshotBytes);

        world.Close();
        Assert.False(ClientChunkContentTransport.TryGet(world, out _));
        Assert.Throws<InvalidOperationException>(() => ClientChunkContentTransport.For(world));
        queue.Dispose();
        queue.Dispose();
        message.Dispose();
        message.Dispose();
        Assert.Equal(1, released);
        Assert.Equal(0, queue.BufferedSnapshotBytes);
        Assert.Throws<ObjectDisposedException>(() => new ClientChunkContentTransport(world));
    }

    [Fact]
    public void AcceptedReservedSnapshotCannotReturnTheSameSlotOrInvokeAbandonmentTwice()
    {
        var world = new WorldRuntimeContext(RuntimeSide.Client, false);
        using var connections = new WorldConnections(world);
        connections.SetAuthority(new WorldConnection());
        using var endpoint = new WorldMessageEndpoint(world, new ContentSink());
        using var queue = new ClientChunkContentTransport(world);
        var allocation = new ChunkAllocationId(Point2.Zero, 1);
        queue.Request([new ChunkContentRequest(allocation)]);
        var abandoned = 0;
        var reservation = Assert.IsType<ClientSnapshotReservation>(queue.TryReserve(allocation, () => abandoned++));
        var snapshot = Snapshot(allocation);
        queue.Receive(snapshot, reservation);
        reservation.Dispose();
        reservation.Dispose();
        Assert.Equal(0, abandoned);
        Assert.Equal(ClientChunkContentTransport.SnapshotBytes, queue.BufferedSnapshotBytes);
        var received = new List<ClientChunkSnapshot>();
        Assert.Equal(1, queue.DrainReceived(received));
        Assert.Same(snapshot, Assert.Single(received));
        Assert.Equal(0, queue.BufferedSnapshotBytes);
        world.Close();
    }

    [Fact]
    public void DisposingThePreviousQueueCannotUnbindAReplacementInTheSameLiveWorld()
    {
        var world = new WorldRuntimeContext(RuntimeSide.Client, false);
        using var endpoint = new WorldMessageEndpoint(world, new ContentSink());
        var previous = new ClientChunkContentTransport(world);
        Assert.Throws<InvalidOperationException>(() => new ClientChunkContentTransport(world));
        Assert.Same(previous, ClientChunkContentTransport.For(world));
        previous.Dispose();
        using var current = new ClientChunkContentTransport(world);
        previous.Dispose();
        Assert.Same(current, ClientChunkContentTransport.For(world));
        world.Close();
    }

    [Fact]
    public void AllocationDiscardWithdrawsOnlyThatLeaseThroughTheExistingMessageSink()
    {
        var world = new WorldRuntimeContext(RuntimeSide.Client, false);
        using var connections = new WorldConnections(world);
        connections.SetAuthority(new WorldConnection());
        var sink = new ContentSink();
        using var endpoint = new WorldMessageEndpoint(world, sink);
        using var queue = new ClientChunkContentTransport(world);
        var allocation = new ChunkAllocationId(new Point2(3, 4), 5);
        queue.Request([new ChunkContentRequest(allocation)]);

        queue.Discard(allocation);

        Assert.Equal(allocation, Assert.Single(sink.Withdrawn));
        Assert.False(queue.IsRequested(allocation));
        world.Close();
    }

    private static ClientChunkSnapshot Snapshot(ChunkAllocationId allocation) => new(
        allocation,
        1,
        new int[AuthorityChunkSnapshot.CellCount],
        new long[AuthorityChunkSnapshot.ShaftCount]
    );

    private sealed class ContentSink : IWorldMessageSink
    {
        public void Send(IProtocolMessage message, PackageAudience audience)
        {
            Assert.IsType<TerrainChunksRequestPackage>(message);
        }

        public List<ChunkAllocationId> Withdrawn { get; } = [];

        public void DiscardQueuedChunkRequests(ChunkAllocationId allocation)
        {
            Withdrawn.Add(allocation);
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
    }
}
