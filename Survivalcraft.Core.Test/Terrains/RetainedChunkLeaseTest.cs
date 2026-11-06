using Engine.Core.Mathematics;

using Game.Protocol.Terrain;
using Game.Terrains;
using Game.Terrains.Distribution;

namespace Survivalcraft.Core.Test.Terrains;

public sealed class RetainedChunkLeaseTest
{
    [Fact]
    public void ReactivatedCacheRenewsLeaseBuffersDeltasAndAcceptsUnchangedBaseline()
    {
        using var terrain = new Terrain();
        var transport = new RecordingTransport();
        var chunk = terrain.AllocateChunk(0, 0);
        chunk.IsLoaded = true;
        chunk.NetworkContentVersion = 10;
        chunk.Cells[0] = 7;
        var old = new ChunkAllocationId(chunk.Coords, chunk.AllocationGeneration);
        transport.Request([new ChunkContentRequest(old)]);
        var applied = 0;
        var deltas = new ClientTerrainDeltaCoordinator(terrain, _ => applied++);
        var contents = new ClientChunkContentCoordinator(terrain);
        contents.ContentInstalled += current => deltas.OnContentInstalled(current);

        var previousGeneration = TerrainUpdater.RefreshRetainedChunk(terrain, chunk, transport, deltas);
        Assert.Equal(old.Generation, previousGeneration);
        Assert.Contains(old, transport.Discarded);
        Assert.Empty(transport.Requests);

        Assert.True(chunk.IsLoaded);
        Assert.Equal(7, chunk.Cells[0]);
        Assert.NotEqual(old.Generation, chunk.AllocationGeneration);
        Assert.Equal(10, chunk.NetworkContentVersion);
        Assert.True(chunk.NeedsContentValidation);
        Assert.True(chunk.NeedsContentSnapshot);
        Assert.Equal(
            TerrainDeltaApplyResult.Buffered,
            deltas.Receive(new TerrainCellDelta(new Point3(1, 65, 1), 3, 10, 11))
        );
        Assert.Equal(0, applied);
        var cells = new int[AuthorityChunkSnapshot.CellCount];
        var shafts = new long[AuthorityChunkSnapshot.ShaftCount];
        Assert.False(contents.TryInstall(new ClientChunkSnapshot(old, 20, cells, shafts)));
        Assert.False(
            contents.TryInstall(
                new ClientChunkSnapshot(
                    new ChunkAllocationId(chunk.Coords, chunk.AllocationGeneration),
                    9,
                    cells,
                    shafts
                )
            )
        );
        Assert.True(
            contents.TryInstall(
                new ClientChunkSnapshot(
                    new ChunkAllocationId(chunk.Coords, chunk.AllocationGeneration),
                    10,
                    cells,
                    shafts
                )
            )
        );
        Assert.Equal(1, applied);
        Assert.Equal(11, chunk.NetworkContentVersion);
        Assert.False(chunk.NeedsContentSnapshot);
    }

    private sealed class RecordingTransport : IChunkContentTransport
    {
        public readonly List<ChunkContentRequest> Requests = [];

        public readonly List<ChunkAllocationId> Discarded = [];


        public void Request(IReadOnlyList<ChunkContentRequest> requests)
        {
            Requests.AddRange(requests);
        }

        public void Discard(ChunkAllocationId allocation)
        {
            Discarded.Add(allocation);
            Requests.RemoveAll(request => request.Allocation == allocation);
        }

        public int DrainReceived(ICollection<ClientChunkSnapshot> destination, int maximumCount = int.MaxValue) => 0;

        public int DrainDeltas(ICollection<TerrainCellDelta> destination, int maximumCount = 1024) => 0;

        public int DrainResyncRequests(ICollection<ChunkContentResync> destination, int maximumCount = int.MaxValue)
        {
            return 0;
        }
    }
}
