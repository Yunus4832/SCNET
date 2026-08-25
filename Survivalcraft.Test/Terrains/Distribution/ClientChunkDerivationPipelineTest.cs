using Game;
using Game.Terrains;
using Game.Terrains.Distribution;

namespace Survivalcraft.Test.Terrains.Distribution;

public sealed class ClientChunkDerivationPipelineTest
{
    [Fact]
    public void GeometryIsCurrentOnlyAfterCompletingInstalledContentVersion()
    {
        var chunk = new TerrainChunk(null!, 1, 2)
        {
            IsLoaded = true,
            NetworkContentVersion = 7
        };

        Assert.False(ClientChunkDerivationPipeline.HasCurrentGeometry(TerrainContentRole.Replica, chunk));

        ClientChunkDerivationPipeline.CompleteGeometry(chunk);

        Assert.True(ClientChunkDerivationPipeline.HasCurrentGeometry(TerrainContentRole.Replica, chunk));
        Assert.True(ClientChunkDerivationPipeline.HasCurrentGeometry(TerrainContentRole.Authority, chunk));
    }

    [Fact]
    public void NewContentInvalidatesTargetAndLoadedNeighborGeometryVersions()
    {
        var terrain = new Terrain();
        var target = terrain.AllocateChunk(0, 0);
        var neighbor = terrain.AllocateChunk(1, 0);
        target.IsLoaded = true;
        target.NetworkContentVersion = 4;
        target.ClientGeometryContentVersion = 3;
        neighbor.IsLoaded = true;
        neighbor.MainThreadState = TerrainChunkState.Valid;
        neighbor.WorkerState = TerrainChunkState.Valid;
        neighbor.ClientGeometryContentVersion = 8;

        new ClientChunkDerivationPipeline(terrain).Begin(target);

        Assert.Equal(TerrainChunkState.InvalidLight, target.MainThreadState);
        Assert.Equal(TerrainChunkState.InvalidLight, target.WorkerState);
        Assert.Equal(0, target.ClientGeometryContentVersion);
        Assert.Equal(TerrainChunkState.InvalidLight, neighbor.MainThreadState);
        Assert.Equal(8, neighbor.ClientGeometryContentVersion);
        Assert.True(neighbor.HasQueuedWorkerDowngrade);
    }

    [Fact]
    public void ClientKeepsCurrentUploadedGeometryVisibleDuringDerivedRebuild()
    {
        var chunk = new TerrainChunk(null!, 0, 0)
        {
            MainThreadState = TerrainChunkState.InvalidLight,
            IsLoaded = true,
            NetworkContentVersion = 6,
            ClientGeometryContentVersion = 6,
            NetworkGeometryUploaded = true
        };

        Assert.True(ClientChunkDerivationPipeline.CanDraw(TerrainContentRole.Replica, chunk));

        chunk.NetworkContentVersion = 7;
        Assert.False(ClientChunkDerivationPipeline.CanDraw(TerrainContentRole.Replica, chunk));
    }
}
