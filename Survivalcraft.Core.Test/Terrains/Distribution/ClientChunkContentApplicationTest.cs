using Engine.Core.Mathematics;

using Game.Protocol.Terrain;
using Game.Terrains;
using Game.Terrains.Distribution;

namespace Survivalcraft.Core.Test.Terrains.Distribution;

public sealed class ClientChunkContentApplicationTest
{
    [Fact]
    public void TimeBudgetKeepsRemainingDeliveriesAndAlternatesSnapshotsWithDeltas()
    {
        using var terrain = new Terrain();
        var first = terrain.AllocateChunk(0, 0);
        var second = terrain.AllocateChunk(1, 0);
        var transport = new QueuedTransport();
        transport.Snapshots.Enqueue(Snapshot(first, 10));
        transport.Snapshots.Enqueue(Snapshot(second, 10));
        transport.Deltas.Enqueue(new TerrainCellDelta(new Point3(1, 64, 1), 7, 10, 11));
        var clock = new ManualTimeProvider();
        var contents = new ClientChunkContentCoordinator(terrain);
        contents.ContentInstalled += _ => clock.Advance();
        var deltas = new ClientTerrainDeltaCoordinator(terrain, _ => clock.Advance());
        contents.ContentInstalled += chunk => deltas.OnContentInstalled(chunk);
        var application = new ClientChunkContentApplication(transport, contents, deltas, clock);

        Assert.Equal(1, application.Pump());
        Assert.True(first.IsLoaded);
        Assert.False(second.IsLoaded);
        Assert.Single(transport.Snapshots);
        Assert.Equal(1, application.Pump());
        Assert.Equal(11, first.NetworkContentVersion);
        Assert.False(second.IsLoaded);
        Assert.Equal(1, application.Pump());
        Assert.True(second.IsLoaded);
        Assert.Empty(transport.Snapshots);
        Assert.Equal(0, application.Pump());
    }

    [Fact]
    public void RecoveryFloorPrecedesBaselineAcrossSeparateOperationBudgets()
    {
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        var allocation = new ChunkAllocationId(chunk.Coords, chunk.AllocationGeneration);
        var transport = new QueuedTransport();
        transport.Resyncs.Enqueue(new ChunkContentResync(allocation, 12));
        transport.Snapshots.Enqueue(Snapshot(chunk, 12));
        var contents = new ClientChunkContentCoordinator(terrain);
        var deltas = new ClientTerrainDeltaCoordinator(terrain, _ => throw new InvalidOperationException());
        contents.ContentInstalled += current => deltas.OnContentInstalled(current);
        var application = new ClientChunkContentApplication(transport, contents, deltas);

        Assert.Equal(1, application.Pump(1));
        Assert.False(chunk.IsLoaded);
        Assert.True(chunk.NeedsContentSnapshot);
        Assert.Single(transport.Snapshots);
        Assert.Equal(1, application.Pump(1));
        Assert.True(chunk.IsLoaded);
        Assert.False(chunk.NeedsContentSnapshot);
        Assert.Equal(12, chunk.NetworkContentVersion);
    }

    private static ClientChunkSnapshot Snapshot(TerrainChunk chunk, long version) =>
        new(
            new ChunkAllocationId(chunk.Coords, chunk.AllocationGeneration),
            version,
            new int[AuthorityChunkSnapshot.CellCount],
            new long[AuthorityChunkSnapshot.ShaftCount]
        );

    private sealed class ManualTimeProvider : TimeProvider
    {
        private long _timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _timestamp;
        public void Advance() => _timestamp += TimeSpan.FromMilliseconds(5).Ticks;
    }

    private sealed class QueuedTransport : IChunkContentTransport
    {
        public Queue<ClientChunkSnapshot> Snapshots { get; } = new();

        public Queue<TerrainCellDelta> Deltas { get; } = new();

        public Queue<ChunkContentResync> Resyncs { get; } = new();

        public void Request(IReadOnlyList<ChunkContentRequest> requests) => throw new NotSupportedException();
        public void Discard(ChunkAllocationId allocation) => throw new NotSupportedException();

        public int DrainReceived(ICollection<ClientChunkSnapshot> destination, int maximumCount = int.MaxValue) =>
            Drain(Snapshots, destination, maximumCount);

        public int DrainDeltas(ICollection<TerrainCellDelta> destination, int maximumCount = 1024) =>
            Drain(Deltas, destination, maximumCount);

        public int DrainResyncRequests(ICollection<ChunkContentResync> destination, int maximumCount = int.MaxValue) =>
            Drain(Resyncs, destination, maximumCount);

        private static int Drain<T>(Queue<T> source, ICollection<T> destination, int maximum)
        {
            var count = 0;
            while (count < maximum && source.TryDequeue(out var item))
            {
                destination.Add(item);
                count++;
            }

            return count;
        }
    }
}
