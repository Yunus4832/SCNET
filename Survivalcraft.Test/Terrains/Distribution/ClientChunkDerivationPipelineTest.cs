using Game.Terrains;
using Game.Terrains.Distribution;
using Game.Terrains.Rendering;

namespace Survivalcraft.Test.Terrains.Distribution;

public sealed class ClientChunkDerivationPipelineTest
{
    [Fact]
    public void GeometryIsCurrentOnlyAfterCompletingInstalledContentVersion()
    {
        using var presentations = new TerrainChunkPresentationStore();
        var chunk = new TerrainChunk(null!, 1, 2)
        {
            IsLoaded = true,
            NetworkContentVersion = 7
        };


        Assert.False(
            ClientChunkDerivationPipeline.HasCurrentGeometry(
                TerrainContentRole.Replica,
                chunk,
                presentations.Get(chunk)
            )
        );

        TerrainMeshTestData.Complete(chunk, presentations.Get(chunk));

        Assert.True(
            ClientChunkDerivationPipeline.HasCurrentGeometry(
                TerrainContentRole.Replica,
                chunk,
                presentations.Get(chunk)
            )
        );
        Assert.True(
            ClientChunkDerivationPipeline.HasCurrentGeometry(
                TerrainContentRole.Authority,
                chunk,
                presentations.Get(chunk)
            )
        );
    }

    [Fact]
    public void NewContentInvalidatesTargetAndLoadedNeighborGeometryVersions()
    {
        using var presentations = new TerrainChunkPresentationStore();
        var terrain = new Terrain();
        var target = terrain.AllocateChunk(0, 0);
        var neighbor = terrain.AllocateChunk(1, 0);
        target.IsLoaded = true;
        target.NetworkContentVersion = 4;
        TerrainMeshTestData.CompleteAtVersion(target, presentations.Get(target), 3);
        neighbor.IsLoaded = true;
        neighbor.SetMainThreadState(TerrainChunkState.Ready);
        neighbor.SetWorkerState(TerrainChunkState.Ready);
        TerrainMeshTestData.CompleteAtVersion(neighbor, presentations.Get(neighbor), 8);

        TerrainUpdater.ResetInstalledChunkLighting(terrain, target);
        new ClientChunkDerivationPipeline(presentations).Begin(target);

        Assert.Equal(TerrainChunkState.InvalidLight, target.MainThreadState);
        Assert.Equal(TerrainChunkState.InvalidLight, target.WorkerState);
        Assert.Equal(3, presentations.Get(target).ClientGeometryContentVersion);
        Assert.Equal(TerrainChunkState.InvalidLight, neighbor.MainThreadState);
        Assert.Equal(8, presentations.Get(neighbor).ClientGeometryContentVersion);
        Assert.True(TerrainChunkStateExchange.ReceiveOnMainThread([neighbor]));
        TerrainChunkStateExchange.ExchangeOnWorkerThread([neighbor]);
        Assert.Equal(TerrainChunkState.InvalidLight, neighbor.WorkerState);
    }

    [Fact]
    public void ClientKeepsLastUploadedGeometryVisibleDuringDerivedRebuild()
    {
        using var presentations = new TerrainChunkPresentationStore();
        var chunk = new TerrainChunk(null!, 0, 0)
        {
            IsLoaded = true,
            NetworkContentVersion = 6,
        };
        chunk.SetMainThreadState(TerrainChunkState.InvalidLight);
        TerrainMeshTestData.CompleteAtVersion(chunk, presentations.Get(chunk), 6);
        presentations.Get(chunk).GeometryUploaded = true;

        Assert.True(ClientChunkDerivationPipeline.CanDraw(TerrainContentRole.Replica, chunk, presentations.Get(chunk)));

        chunk.NetworkContentVersion = 7;
        Assert.False(
            ClientChunkDerivationPipeline.HasCurrentGeometry(
                TerrainContentRole.Replica,
                chunk,
                presentations.Get(chunk)
            )
        );
        Assert.True(ClientChunkDerivationPipeline.CanDraw(TerrainContentRole.Replica, chunk, presentations.Get(chunk)));
    }

    [Fact]
    public void ClientDoesNotDrawInitialContentBeforeGeometryUpload()
    {
        using var presentations = new TerrainChunkPresentationStore();
        var chunk = new TerrainChunk(null!, 0, 0)
        {
            IsLoaded = true,
            NetworkContentVersion = 1,
        };
        TerrainMeshTestData.CompleteAtVersion(chunk, presentations.Get(chunk), 1);
        presentations.Get(chunk).GeometryUploaded = false;

        Assert.True(
            ClientChunkDerivationPipeline.HasCurrentGeometry(
                TerrainContentRole.Replica,
                chunk,
                presentations.Get(chunk)
            )
        );
        Assert.False(
            ClientChunkDerivationPipeline.CanDraw(TerrainContentRole.Replica, chunk, presentations.Get(chunk))
        );
    }

    [Fact]
    public void GuiAuthorityKeepsUploadedGeometryVisibleDuringDerivedRebuild()
    {
        using var presentations = new TerrainChunkPresentationStore();
        var chunk = new TerrainChunk(null!, 0, 0);
        chunk.SetMainThreadState(TerrainChunkState.InvalidLight);
        presentations.Get(chunk).GeometryUploaded = true;

        Assert.True(
            ClientChunkDerivationPipeline.CanDraw(TerrainContentRole.Authority, chunk, presentations.Get(chunk))
        );

        presentations.Get(chunk).GeometryUploaded = false;
        Assert.False(
            ClientChunkDerivationPipeline.CanDraw(TerrainContentRole.Authority, chunk, presentations.Get(chunk))
        );
    }
}
