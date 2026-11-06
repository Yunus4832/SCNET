using Engine.Core.Mathematics;

using Game.Terrains;
using Game.Terrains.Distribution;
using Game.Terrains.Rendering;

namespace Survivalcraft.Test.Terrains.Distribution;

public sealed class ClientChunkContentCoordinatorTest
{
    [Fact]
    public void RequestedRefreshReplacesOnlyTheCurrentAllocationAndKeepsUploadedGeometryDuringRebuild()
    {
        using var presentations = new TerrainChunkPresentationStore();
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        var content = new ClientChunkContentCoordinator(terrain);
        content.ContentInstalled += chunk =>
        {
            TerrainUpdater.ResetInstalledChunkLighting(terrain, chunk);
            new ClientChunkDerivationPipeline(presentations).Begin(chunk);
        };
        Assert.True(content.TryInstall(Snapshot(chunk.Coords, chunk.AllocationGeneration, 10, 20)));
        chunk.SetMainThreadState(TerrainChunkState.Ready);
        chunk.SetWorkerState(TerrainChunkState.Ready);
        presentations.Get(chunk).GeometryUploaded = true;
        TerrainMeshTestData.Complete(chunk, presentations.Get(chunk));
        var deltas = new ClientTerrainDeltaCoordinator(terrain, _ => { });
        content.ContentInstalled += target => deltas.OnContentInstalled(target);
        Assert.Equal(
            TerrainDeltaApplyResult.ResyncRequired,
            deltas.RequireSnapshot(
                new ChunkContentResync(new ChunkAllocationId(chunk.Coords, chunk.AllocationGeneration), 12)
            )
        );
        Assert.Equal(20, chunk.Cells[0]);
        Assert.True(ClientChunkDerivationPipeline.CanDraw(TerrainContentRole.Replica, chunk, presentations.Get(chunk)));
        Assert.False(content.TryInstall(Snapshot(chunk.Coords, chunk.AllocationGeneration, 10, 99)));
        Assert.False(content.TryInstall(Snapshot(chunk.Coords, chunk.AllocationGeneration, 9, 99)));
        Assert.False(content.TryInstall(Snapshot(chunk.Coords, chunk.AllocationGeneration + 1, 12, 99)));
        Assert.True(content.TryInstall(Snapshot(chunk.Coords, chunk.AllocationGeneration, 11, 30)));
        Assert.True(chunk.NeedsContentSnapshot);
        Assert.True(ClientChunkDerivationPipeline.CanDraw(TerrainContentRole.Replica, chunk, presentations.Get(chunk)));
        Assert.True(content.TryInstall(Snapshot(chunk.Coords, chunk.AllocationGeneration, 12, 40)));
        Assert.False(chunk.NeedsContentSnapshot);
        Assert.True(chunk.IsLoaded);
        Assert.Equal(40, chunk.Cells[0]);
        Assert.Equal(TerrainChunkState.InvalidLight, chunk.MainThreadState);
        Assert.True(ClientChunkDerivationPipeline.CanDraw(TerrainContentRole.Replica, chunk, presentations.Get(chunk)));
        Assert.False(
            ClientChunkDerivationPipeline.HasCurrentGeometry(
                TerrainContentRole.Replica,
                chunk,
                presentations.Get(chunk)
            )
        );
        Assert.False(content.TryInstall(Snapshot(chunk.Coords, chunk.AllocationGeneration, 13, 50)));
    }

    private static ClientChunkSnapshot Snapshot(
        Point2 coords,
        ulong generation,
        long version,
        int value
    )
    {
        var cells = new int[AuthorityChunkSnapshot.CellCount];
        cells[0] = value;
        return new ClientChunkSnapshot(
            new ChunkAllocationId(coords, generation),
            version,
            cells,
            new long[AuthorityChunkSnapshot.ShaftCount]
        );
    }
}
