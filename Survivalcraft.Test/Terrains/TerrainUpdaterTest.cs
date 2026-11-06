using Game.Terrains;
using Game.Terrains.Distribution;
using Game.Terrains.Rendering;

namespace Survivalcraft.Test.Terrains;

public sealed class TerrainUpdaterTest
{
    [Fact]
    public void ChunkGeometryUsesSixteenSlicesFor256BlockHeight()
    {
        using var presentations = new TerrainChunkPresentationStore();
        var chunk = new TerrainChunk(null!, 1, 2);

        Assert.Equal(16, TerrainChunk.SlicesCount);
        Assert.Equal(TerrainChunk.SlicesCount, presentations.Get(chunk).ChunkSliceGeometries.Length);
        Assert.Equal(TerrainChunk.SlicesCount, presentations.Get(chunk).SliceContentsHashes.Length);
        Assert.Equal(TerrainChunk.SlicesCount, presentations.Get(chunk).GeneratedSliceContentsHashes.Length);
    }

    [Fact]
    public void CompletedBackgroundGeometryBecomesValidOnMainThread()
    {
        using var presentations = new TerrainChunkPresentationStore();
        var chunk = new TerrainChunk(null!, 1, 2);
        chunk.SetMainThreadState(TerrainChunkState.InvalidLight);
        chunk.SetWorkerState(TerrainChunkState.Ready);
        presentations.Get(chunk).NewGeometryData = true;
        TerrainChunkStateExchange.ExchangeOnWorkerThread([chunk]);

        var downgraded = TerrainChunkStateExchange.ReceiveOnMainThread([chunk]);

        Assert.False(downgraded);
        Assert.Equal(TerrainChunkState.Ready, chunk.MainThreadState);
    }

    [Fact]
    public void ClientDerivationAcceptsTheInstalledLightingState()
    {
        using var presentations = new TerrainChunkPresentationStore();
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(1, 2);
        chunk.IsLoaded = true;
        chunk.SetMainThreadState(TerrainChunkState.InvalidLight);
        chunk.SetWorkerState(TerrainChunkState.InvalidLight);
        new ClientChunkDerivationPipeline(presentations).Begin(chunk);
        Assert.Equal(TerrainChunkState.InvalidLight, chunk.MainThreadState);
        Assert.Equal(TerrainChunkState.InvalidLight, chunk.WorkerState);
    }

    [Fact]
    public void NetworkChunkStallClassificationDistinguishesPipelineStages()
    {
        using var presentations = new TerrainChunkPresentationStore();
        var chunk = new TerrainChunk(null!, 1, 2)
        {
            IsRequested = true,
            NetworkRequestTime = 10.0
        };


        Assert.True(TerrainMeshUpdater.IsNetworkChunkStalled(chunk, 15.0, presentations.Get(chunk)));

        chunk.IsRequested = false;
        chunk.IsLoaded = true;
        chunk.NetworkContentReceiveTime = 20.0;
        chunk.NetworkContentVersion = 2;
        TerrainMeshTestData.CompleteAtVersion(chunk, presentations.Get(chunk), 2);
        chunk.SetWorkerState(TerrainChunkState.Ready);
        chunk.SetMainThreadState(TerrainChunkState.Ready);
        Assert.True(TerrainMeshUpdater.IsNetworkChunkStalled(chunk, 25.0, presentations.Get(chunk)));

        presentations.Get(chunk).GeometryUploaded = true;
        Assert.False(TerrainMeshUpdater.IsNetworkChunkStalled(chunk, 25.0, presentations.Get(chunk)));

        chunk.SetWorkerState(TerrainChunkState.InvalidPropagatedLight);
        chunk.SetMainThreadState(TerrainChunkState.InvalidPropagatedLight);
        Assert.False(TerrainMeshUpdater.IsNetworkChunkStalled(chunk, 25.0, presentations.Get(chunk)));
    }
}
