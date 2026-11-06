using Engine.Core.Mathematics;

using Game.Terrains;
using Game.Terrains.Distribution;
using Game.Terrains.Rendering;

namespace Survivalcraft.Test.Terrains.Distribution;

public sealed class ClientTerrainDeltaCoordinatorTest
{
    [Fact]
    public void VersionGapRequestsSnapshotWhileRetainingInstalledCollisionAndGeometry()
    {
        using var presentations = new TerrainChunkPresentationStore();
        var terrain = new Terrain();
        var chunk = LoadedChunk(terrain, 10);
        TerrainMeshTestData.CompleteAtVersion(chunk, presentations.Get(chunk), 10);
        presentations.Get(chunk).GeometryUploaded = true;
        var coordinator = new ClientTerrainDeltaCoordinator(terrain, _ => { });

        var result = coordinator.Receive(Delta(11, 12, 42));

        Assert.Equal(TerrainDeltaApplyResult.ResyncRequired, result);
        Assert.True(chunk.IsLoaded);
        Assert.True(chunk.NeedsContentSnapshot);
        Assert.False(chunk.IsRequested);
        Assert.Equal(TerrainChunkState.Ready, chunk.MainThreadState);
        Assert.Equal(TerrainChunkState.Ready, chunk.WorkerState);
        Assert.True(presentations.Get(chunk).GeometryUploaded);
        Assert.Equal(10, presentations.Get(chunk).ClientGeometryContentVersion);
        Assert.Equal(10, chunk.NetworkContentVersion);

        chunk.IsLoaded = true;
        chunk.NetworkContentVersion = 12;
        Assert.Equal(TerrainDeltaApplyResult.Ignored, coordinator.OnContentInstalled(chunk));
        Assert.True(chunk.IsLoaded);
        Assert.False(chunk.NeedsContentSnapshot);
    }


    private static TerrainChunk LoadedChunk(Terrain terrain, long version)
    {
        var chunk = terrain.AllocateChunk(0, 0);
        chunk.IsLoaded = true;
        chunk.NetworkContentVersion = version;
        chunk.SetMainThreadState(TerrainChunkState.Ready);
        chunk.SetWorkerState(TerrainChunkState.Ready);
        return chunk;
    }

    private static TerrainCellDelta Delta(long baseVersion, long resultVersion, int value) =>
        new(new Point3(1, 2, 3), value, baseVersion, resultVersion);
}
