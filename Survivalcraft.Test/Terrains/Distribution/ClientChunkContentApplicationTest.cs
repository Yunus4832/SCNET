using Engine.Core.Mathematics;

using EntitySystem.Core;

using Game.Network;
using Game.Terrains;
using Game.Terrains.Distribution;

namespace Survivalcraft.Test.Terrains.Distribution;

public sealed class ClientChunkContentApplicationTest
{
    [Fact]
    public void TimeBudgetRetainsUndrainedSnapshotsAndAlternatesThemWithDeltas()
    {
        using var project = new Project();
        var node = new NetNode(
            NetNodeOptions.Default,
            true,
            Game.Network.Handling.PackageHandlerCatalog.Empty,
            NodeRole.Client
        );
        node.Runtime.BindProject(project);
        node.Server = new Client(Guid.Empty);
        var transport = new ClientChunkContentTransport(node.Runtime);
        using var terrain = new Terrain();
        var first = terrain.AllocateChunk(0, 0);
        var second = terrain.AllocateChunk(1, 0);
        var clock = new ManualTimeProvider();
        var contents = new ClientChunkContentCoordinator(terrain);
        contents.ContentInstalled += _ => clock.Advance();
        var deltas = new ClientTerrainDeltaCoordinator(terrain, _ => clock.Advance());
        contents.ContentInstalled += chunk => deltas.OnContentInstalled(chunk);
        var application = new ClientChunkContentApplication(transport, contents, deltas, clock);
        transport.Request([new ChunkContentRequest(Allocation(first)), new ChunkContentRequest(Allocation(second))]);
        transport.Receive(Snapshot(first, 10));
        transport.Receive(Snapshot(second, 10));
        transport.Receive(new TerrainCellDelta(new Point3(1, 70, 1), 0, 10, 11));
        var queuedBytes = transport.PendingSnapshotBytes;

        Assert.Equal(1, application.Pump());
        Assert.True(first.IsLoaded);
        Assert.False(second.IsLoaded);
        Assert.Equal(queuedBytes / 2, transport.PendingSnapshotBytes);
        Assert.Equal(1, application.Pump());
        Assert.Equal(11, first.NetworkContentVersion);
        Assert.False(second.IsLoaded);
        Assert.Equal(1, application.Pump());
        Assert.True(second.IsLoaded);
        Assert.Equal(0, transport.PendingSnapshotBytes);
        Assert.Equal(0, application.Pump());
    }

    [Fact]
    public void RecoveryFloorIsAppliedBeforeSnapshotsEvenWhenOperationsSpanSeveralUpdates()
    {
        using var project = new Project();
        var node = new NetNode(
            NetNodeOptions.Default,
            true,
            Game.Network.Handling.PackageHandlerCatalog.Empty,
            NodeRole.Client
        );
        node.Runtime.BindProject(project);
        node.Server = new Client(Guid.Empty);
        var transport = new ClientChunkContentTransport(node.Runtime);
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        transport.Request([new ChunkContentRequest(Allocation(chunk))]);
        for (var version = 1; version <= 1000; version++)
        {
            transport.Receive(new TerrainCellDelta(new Point3(1, 70, 1), 0, version, version + 1));
        }

        transport.Receive(Snapshot(chunk, 500));
        var contents = new ClientChunkContentCoordinator(terrain);
        var deltas = new ClientTerrainDeltaCoordinator(
            terrain,
            _ => throw new InvalidOperationException("Lost chain.")
        );
        contents.ContentInstalled += target => deltas.OnContentInstalled(target);
        var application = new ClientChunkContentApplication(transport, contents, deltas);
        Assert.Equal(1, application.Pump(1));
        Assert.False(chunk.IsLoaded);
        Assert.True(chunk.NeedsContentSnapshot);
        Assert.True(transport.PendingSnapshotBytes > 0);
        Assert.Equal(1, application.Pump(1));
        Assert.True(chunk.IsLoaded);
        Assert.True(chunk.NeedsContentSnapshot);
        transport.Receive(Snapshot(chunk, 1001));
        Assert.Equal(1, application.Pump(1));
        Assert.False(chunk.NeedsContentSnapshot);
        Assert.Equal(1001, chunk.NetworkContentVersion);
    }

    private static ChunkAllocationId Allocation(TerrainChunk chunk) => new(chunk.Coords, chunk.AllocationGeneration);

    private static ClientChunkSnapshot Snapshot(TerrainChunk chunk, long version)
    {
        return new ClientChunkSnapshot(
            Allocation(chunk),
            version,
            new int[AuthorityChunkSnapshot.CellCount],
            new long[AuthorityChunkSnapshot.ShaftCount]
        );
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private long _timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _timestamp;

        internal void Advance() => _timestamp += TimeSpan.FromMilliseconds(5).Ticks;
    }
}
