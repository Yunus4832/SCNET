using Game;
using Game.Network.Enums;
using Game.Terrains;
using Game.TerrainSerializers;

namespace Survivalcraft.Test.Terrains;

public sealed class TerrainUpdaterTest
{
    [Fact]
    public void ChunkGeometryUsesSixteenSlicesFor256BlockHeight()
    {
        var chunk = new TerrainChunk(null!, 1, 2);

        Assert.Equal(16, TerrainChunk.SlicesCount);
        Assert.Equal(TerrainChunk.SlicesCount, chunk.ChunkSliceGeometries.Length);
        Assert.Equal(TerrainChunk.SlicesCount, chunk.SliceContentsHashes.Length);
        Assert.Equal(TerrainChunk.SlicesCount, chunk.GeneratedSliceContentsHashes.Length);
    }

    [Fact]
    public void SeedGeneratedBasisMovesArraysOnlyOnce()
    {
        var cells = new int[16 * 16 * 256];
        var shafts = new long[16 * 16];
        cells[123] = 456;
        shafts[12] = 789;
        var basis = new SeedGeneratedChunkBasis(cells, shafts);
        var first = new TerrainChunk(null!, 1, 2);
        var second = new TerrainChunk(null!, 1, 2);

        Assert.True(basis.TryMoveTo(first));
        Assert.Same(cells, first.Cells);
        Assert.Same(shafts, first.Shafts);
        Assert.Equal(456, first.Cells[123]);
        Assert.Equal(789, first.Shafts[12]);
        Assert.False(basis.TryMoveTo(second));
        Assert.NotSame(cells, second.Cells);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(4, 3)]
    [InlineData(8, 4)]
    public void SeedTerrainParallelismLeavesCapacityForOtherWork(int processorCount, int expected)
    {
        Assert.Equal(expected, SeedTerrainGenerationPolicy.GetParallelism(processorCount));
    }

    [Fact]
    public void DeferredChunkDiscardKeepsLocationUpdatePending()
    {
        Assert.False(TerrainUpdater.CanCompleteLocationUpdate(true));
        Assert.True(TerrainUpdater.CanCompleteLocationUpdate(false));
    }

    [Fact]
    public void ClientOnlySynchronouslyUpdatesChunksAfterNetworkContentArrives()
    {
        var chunk = new TerrainChunk(null!, 1, 2);

        Assert.False(TerrainUpdater.CanSynchronouslyUpdateChunk(WorkType.Client, chunk));
        Assert.True(TerrainUpdater.CanSynchronouslyUpdateChunk(WorkType.Local, chunk));

        chunk.IsLoaded = true;

        Assert.True(TerrainUpdater.CanSynchronouslyUpdateChunk(WorkType.Client, chunk));
    }

    [Fact]
    public void CompletedBackgroundGeometryBecomesValidOnMainThread()
    {
        var chunk = new TerrainChunk(null!, 1, 2)
        {
            State = TerrainChunkState.InvalidLight,
            ThreadState = TerrainChunkState.Valid,
            UpgradedState = TerrainChunkState.Valid,
            WasUpgraded = true,
            NewGeometryData = true
        };

        var downgraded = TerrainUpdater.ReceiveMainThreadChunkStates([chunk]);

        Assert.False(downgraded);
        Assert.Equal(TerrainChunkState.Valid, chunk.State);
        Assert.Null(chunk.UpgradedState);
    }
}
