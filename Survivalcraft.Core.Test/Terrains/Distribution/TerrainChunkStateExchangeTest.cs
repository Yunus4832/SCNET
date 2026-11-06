using Game.Terrains;
using Game.Terrains.Distribution;

namespace Survivalcraft.Core.Test.Terrains.Distribution;

public sealed class TerrainChunkStateExchangeTest
{
    [Fact]
    public void RepeatedDowngradesKeepLowestMainThreadState()
    {
        var chunk = new TerrainChunk(null!, 0, 0);
        chunk.SetMainThreadState(TerrainChunkState.Ready);
        chunk.SetWorkerState(TerrainChunkState.Ready);

        TerrainChunkStateExchange.RequestDowngrade(chunk, TerrainChunkState.NotLoaded);
        TerrainChunkStateExchange.RequestDowngrade(chunk, TerrainChunkState.InvalidLight);
        TerrainChunkStateExchange.ExchangeOnWorkerThread([chunk]);

        Assert.Equal(TerrainChunkState.NotLoaded, chunk.MainThreadState);
        Assert.Equal(TerrainChunkState.NotLoaded, chunk.WorkerState);
    }

    [Fact]
    public void PendingDowngradeDiscardsOlderWorkerPublication()
    {
        var chunk = new TerrainChunk(null!, 0, 0);
        chunk.SetMainThreadState(TerrainChunkState.Ready);
        chunk.SetWorkerState(TerrainChunkState.Ready);
        TerrainChunkStateExchange.ExchangeOnWorkerThread([chunk]);
        TerrainChunkStateExchange.RequestDowngrade(chunk, TerrainChunkState.InvalidLight);

        Assert.True(TerrainChunkStateExchange.ReceiveOnMainThread([chunk]));
        TerrainChunkStateExchange.ExchangeOnWorkerThread([chunk]);
        TerrainChunkStateExchange.ExchangeOnWorkerThread([chunk]);
        Assert.False(TerrainChunkStateExchange.ReceiveOnMainThread([chunk]));

        Assert.Equal(TerrainChunkState.InvalidLight, chunk.MainThreadState);
        Assert.Equal(TerrainChunkState.InvalidLight, chunk.WorkerState);
    }
}
