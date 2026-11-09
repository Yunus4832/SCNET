using Engine.Core.Mathematics;

using EntitySystem.Core;

using Game.Network;
using Game.Network.Transport;
using Game.Protocol;
using Game.Protocol.Terrain;
using Game.Terrains;
using Game.Terrains.Distribution;
using Game.World;

namespace Survivalcraft.Core.Test.Terrains.Distribution;

public sealed class ClientChunkContentTransportPrefixTest
{
    [Fact]
    public void OwnedQueuedBaselineCoversThreeHundredLatePrefixDeltasWithoutOverflow()
    {
        using var fixture = new Fixture();
        var allocation = new ChunkAllocationId(Point2.Zero, 1);
        fixture.Request(allocation);
        fixture.Queue.Receive(Snapshot(allocation, 400));
        for (var version = 1; version <= 300; version++) { fixture.Queue.Receive(Delta(allocation, version)); }

        Assert.Equal(0, fixture.Queue.PendingDeltaCount);
        Assert.Equal(0, fixture.Queue.PendingResyncCount);
        var deltas = new List<TerrainCellDelta>();
        Assert.Equal(0, fixture.Queue.DrainDeltas(deltas));
        var snapshots = new List<ClientChunkSnapshot>();
        Assert.Equal(1, fixture.Queue.DrainReceived(snapshots));
        Assert.Equal(400, Assert.Single(snapshots).ContentVersion);
        Assert.False(fixture.Queue.HasPendingWork);
    }

    [Fact]
    public void AcceptedBaselineSubtractsOnlyItsCoveredPrefixAndReclaimsAnEmptyBatch()
    {
        using var fixture = new Fixture();
        var first = new ChunkAllocationId(Point2.Zero, 1);
        var other = new ChunkAllocationId(new Point2(1, 0), 1);
        fixture.Request(first, other);
        for (var version = 1; version <= 256; version++) { fixture.Queue.Receive(Delta(first, version)); }

        fixture.Queue.Receive(Delta(other, 1));
        fixture.Queue.Receive(Delta(other, 2));
        Assert.Equal(258, fixture.Queue.PendingDeltaCount);

        fixture.Queue.Receive(Snapshot(first, 201));
        Assert.Equal(58, fixture.Queue.PendingDeltaCount);
        Assert.Equal(0, fixture.Queue.PendingResyncCount);
        fixture.Queue.Receive(Snapshot(first, 257));
        Assert.Equal(2, fixture.Queue.PendingDeltaCount);
        var remaining = new List<TerrainCellDelta>();
        Assert.Equal(2, fixture.Queue.DrainDeltas(remaining));
        Assert.Equal(new long[] { 1, 2 }, remaining.Select(delta => delta.BaseContentVersion));
        Assert.All(remaining, delta => Assert.Equal(other.Coords, Terrain.ToChunk(delta.Cell.X, delta.Cell.Z)));
        Assert.Equal(0, fixture.Queue.PendingDeltaCount);
    }

    [Theory]
    [InlineData(258L, false)]
    [InlineData(300L, false)]
    [InlineData(257L, true)]
    public void BaselineClearsOnlyRecoveryFloorsItActuallyCovers(long snapshotVersion, bool expectRecovery)
    {
        using var fixture = new Fixture();
        var allocation = new ChunkAllocationId(Point2.Zero, 1);
        fixture.Request(allocation);
        Overflow(fixture.Queue, allocation, 1);
        Assert.Equal(1, fixture.Queue.PendingResyncCount);
        fixture.Queue.Receive(Snapshot(allocation, snapshotVersion));
        Assert.Equal(expectRecovery ? 1 : 0, fixture.Queue.PendingResyncCount);
        Assert.Equal(0, fixture.Queue.PendingDeltaCount);
        fixture.Queue.Receive(Delta(allocation, 200));
        Assert.Equal(0, fixture.Queue.PendingDeltaCount);
        if (expectRecovery)
        {
            var recovery = new List<ChunkContentResync>();
            Assert.Equal(1, fixture.Queue.DrainResyncRequests(recovery));
            Assert.Equal(258, Assert.Single(recovery).RequiredContentVersion);
        }
        else
        {
            fixture.Queue.Receive(Delta(allocation, snapshotVersion));
            Assert.Equal(1, fixture.Queue.PendingDeltaCount);
        }
    }

    [Fact]
    public void UncoveredOutOfOrderTailRetainsEveryExactBaseLink()
    {
        using var fixture = new Fixture();
        var allocation = new ChunkAllocationId(Point2.Zero, 1);
        fixture.Request(allocation);
        fixture.Queue.Receive(Delta(allocation, 1000));
        fixture.Queue.Receive(Delta(allocation, 1002));
        fixture.Queue.Receive(Delta(allocation, 1001));
        fixture.Queue.Receive(Delta(allocation, 999));
        fixture.Queue.Receive(Snapshot(allocation, 1000));
        Assert.Equal(3, fixture.Queue.PendingDeltaCount);
        fixture.Queue.Receive(Snapshot(allocation, 999));
        Assert.Equal(3, fixture.Queue.PendingDeltaCount);
        var snapshots = new List<ClientChunkSnapshot>();
        fixture.Queue.DrainReceived(snapshots);
        Assert.Equal(1000, Assert.Single(snapshots).ContentVersion);
        var tail = new List<TerrainCellDelta>();
        Assert.Equal(3, fixture.Queue.DrainDeltas(tail));
        Assert.Equal(new long[] { 1000, 1001, 1002 }, tail.Select(delta => delta.BaseContentVersion));
        Assert.Equal(new long[] { 1001, 1002, 1003 }, tail.Select(delta => delta.ResultContentVersion));
        Assert.Equal(new[] { 1000, 1001, 1002 }, tail.Select(delta => delta.Value));
    }

    [Fact]
    public void OldAllocationSnapshotCannotClearCurrentTailOrRecoveryFloor()
    {
        using var fixture = new Fixture();
        var old = new ChunkAllocationId(Point2.Zero, 1);
        var current = old with { Generation = 2 };
        fixture.Request(old);
        fixture.Queue.Receive(Snapshot(old, 1000));
        fixture.Request(current);
        fixture.Queue.Receive(Snapshot(current, 100));
        fixture.Queue.Receive(Delta(current, 100));
        fixture.Queue.Receive(Snapshot(old, 10000));
        Assert.Equal(1, fixture.Queue.PendingDeltaCount);
        var tail = new List<TerrainCellDelta>();
        fixture.Queue.DrainDeltas(tail);
        Assert.Equal(101, Assert.Single(tail).ResultContentVersion);

        Overflow(fixture.Queue, current, 100);
        Assert.Equal(1, fixture.Queue.PendingResyncCount);
        fixture.Queue.Receive(Snapshot(old, 10000));
        Assert.Equal(1, fixture.Queue.PendingResyncCount);
        var recovery = new List<ChunkContentResync>();
        fixture.Queue.DrainResyncRequests(recovery);
        Assert.Equal(current, Assert.Single(recovery).Allocation);
        Assert.Equal(357, recovery[0].RequiredContentVersion);
        var snapshots = new List<ClientChunkSnapshot>();
        fixture.Queue.DrainReceived(snapshots);
        Assert.Equal(current, Assert.Single(snapshots).Allocation);
        Assert.Equal(100, snapshots[0].ContentVersion);
    }

    [Fact]
    public void QueuedHigherBaselineActuallyInstallsAfterItsRedundantDeltasArePruned()
    {
        using var fixture = new Fixture();
        using var terrain = new Terrain();
        var target = terrain.AllocateChunk(0, 0);
        var allocation = new ChunkAllocationId(target.Coords, target.AllocationGeneration);
        var contents = new ClientChunkContentCoordinator(terrain);
        Assert.True(contents.TryInstall(Snapshot(allocation, 100)));
        Assert.False(target.NeedsContentSnapshot);
        fixture.Request(allocation);
        fixture.Queue.Receive(Snapshot(allocation, 200));
        for (var version = 100; version < 200; version++) { fixture.Queue.Receive(Delta(allocation, version)); }

        Assert.Equal(0, fixture.Queue.PendingDeltaCount);
        var deltas = new ClientTerrainDeltaCoordinator(
            terrain,
            _ => throw new InvalidOperationException("Covered prefix must not be replayed.")
        );
        contents.ContentInstalled += chunk => deltas.OnContentInstalled(chunk);
        var application = new ClientChunkContentApplication(fixture.Queue, contents, deltas);

        Assert.Equal(1, application.Pump(1));
        Assert.Equal(200, target.NetworkContentVersion);
        Assert.Equal(200, target.Cells[0]);
        Assert.False(target.NeedsContentSnapshot);
        Assert.False(fixture.Queue.HasPendingWork);
    }

    private static void Overflow(ClientChunkContentTransport queue, ChunkAllocationId allocation, long firstBase)
    {
        for (var version = firstBase; version <= firstBase + 256; version++)
        {
            queue.Receive(Delta(allocation, version));
        }

        Assert.Equal(0, queue.PendingDeltaCount);
    }

    private static TerrainCellDelta Delta(ChunkAllocationId allocation, long baseVersion)
    {
        return new TerrainCellDelta(
            new Point3(allocation.Coords.X * 16, 64, allocation.Coords.Y * 16),
            (int)baseVersion,
            baseVersion,
            baseVersion + 1
        );
    }

    private static ClientChunkSnapshot Snapshot(ChunkAllocationId allocation, long version)
    {
        var cells = new int[AuthorityChunkSnapshot.CellCount];
        cells[0] = (int)version;
        return new ClientChunkSnapshot(allocation, version, cells, new long[AuthorityChunkSnapshot.ShaftCount]);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly WorldRuntimeContext _world = new(RuntimeSide.Client, false);

        private readonly WorldConnections _connections;

        private readonly WorldMessageEndpoint _endpoint;

        public ClientChunkContentTransport Queue { get; }

        public Fixture()
        {
            _connections = new WorldConnections(_world);
            _connections.SetAuthority(new WorldConnection());
            _endpoint = new WorldMessageEndpoint(_world, new Sink());
            Queue = new ClientChunkContentTransport(_world);
        }

        public void Request(params ChunkAllocationId[] allocations)
        {
            Queue.Request(allocations.Select(allocation => new ChunkContentRequest(allocation)).ToArray());
        }

        public void Dispose()
        {
            _world.Close();
            Queue.Dispose();
            _endpoint.Dispose();
            _connections.Dispose();
        }
    }

    private sealed class Sink : IWorldMessageSink
    {
        public void Send(IProtocolMessage message, PackageAudience audience)
        {
            Assert.IsType<TerrainChunksRequestPackage>(message);
        }

        public void DiscardQueuedChunkRequests(ChunkAllocationId allocation)
        {
        }

        public void SendNearPoint(Vector3 position, float radius, IProtocolMessage message)
        {
            throw new NotSupportedException();
        }

        public void SendToChunkObservers(Point2 chunk, IProtocolMessage message) => throw new NotSupportedException();

        public void SendToObservers(Entity entity, IProtocolMessage message, WorldConnection? except)
        {
            throw new NotSupportedException();
        }

        public bool CanQueueTerrainSnapshot(WorldConnection recipient) => throw new NotSupportedException();

        public bool TryQueueOwnedTerrainSnapshot(WorldConnection recipient, TerrainSnapshotMessage message)
        {
            throw new NotSupportedException();
        }

        public void DiscardOlderChunkAllocations(WorldConnection recipient, ChunkAllocationId allocation)
        {
            throw new NotSupportedException();
        }

        public void TrimChunkDelivery(WorldConnection recipient, Vector2 center, float distance)
        {
            throw new NotSupportedException();
        }

        public void ReleaseChunkDelivery(WorldConnection recipient) => throw new NotSupportedException();
        public void InvalidateChunkDelivery(Point2 coords) => throw new NotSupportedException();
    }
}
