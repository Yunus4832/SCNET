using Engine.Core;

using Game;
using Game.Network;
using Game.Terrains;
using Game.Terrains.Distribution;
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

        Assert.False(TerrainUpdater.CanSynchronouslyUpdateChunk(TerrainContentRole.Replica, chunk));
        Assert.True(TerrainUpdater.CanSynchronouslyUpdateChunk(TerrainContentRole.Authority, chunk));

        chunk.IsLoaded = true;

        Assert.True(TerrainUpdater.CanSynchronouslyUpdateChunk(TerrainContentRole.Replica, chunk));
    }

    [Fact]
    public void CompletedBackgroundGeometryBecomesValidOnMainThread()
    {
        var chunk = new TerrainChunk(null!, 1, 2)
        {
            MainThreadState = TerrainChunkState.InvalidLight,
            WorkerState = TerrainChunkState.Valid,
            NewGeometryData = true
        };
        chunk.PublishWorkerState(TerrainChunkState.Valid);

        var downgraded = TerrainChunkStateExchange.ReceiveOnMainThread([chunk]);

        Assert.False(downgraded);
        Assert.Equal(TerrainChunkState.Valid, chunk.MainThreadState);
    }

    [Fact]
    public void ClientBackgroundUpdaterSkipsChunksAwaitingNetworkContent()
    {
        var awaitingContent = new TerrainChunk(null!, 1, 2);
        var receivedContent = new TerrainChunk(null!, 2, 2)
        {
            WorkerState = TerrainChunkState.InvalidLight,
            IsLoaded = true
        };

        Assert.False(TerrainUpdater.CanBackgroundUpdateChunk(TerrainContentRole.Replica, awaitingContent));
        Assert.True(TerrainUpdater.CanBackgroundUpdateChunk(TerrainContentRole.Replica, receivedContent));
        Assert.True(TerrainUpdater.CanBackgroundUpdateChunk(TerrainContentRole.Authority, awaitingContent));
    }

    [Fact]
    public void InstalledContentBaselineCannotBeOverwrittenByPendingNotLoadedTransition()
    {
        var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(1, 2);
        chunk.MainThreadState = TerrainChunkState.NotLoaded;
        chunk.WorkerState = TerrainChunkState.NotLoaded;
        chunk.IsLoaded = true;
        chunk.QueueWorkerDowngrade(TerrainChunkState.NotLoaded);
        chunk.PublishWorkerState(TerrainChunkState.NotLoaded);

        new ClientChunkDerivationPipeline(terrain).Begin(chunk);
        TerrainChunkStateExchange.ReceiveOnMainThread([chunk]);

        Assert.Equal(TerrainChunkState.InvalidLight, chunk.MainThreadState);
        Assert.Equal(TerrainChunkState.InvalidLight, chunk.WorkerState);
        Assert.False(chunk.HasQueuedWorkerDowngrade);
    }

    [Fact]
    public void NetworkChunkRequestRetriesOnlyAfterRecoveryWindow()
    {
        var chunk = new TerrainChunk(null!, 1, 2)
        {
            IsRequested = true,
            NetworkRequestTime = 10.0
        };

        Assert.False(TerrainUpdater.ShouldRequestNetworkChunk(chunk, 14.999));
        Assert.True(TerrainUpdater.ShouldRequestNetworkChunk(chunk, 15.0));
    }

    [Fact]
    public void NetworkChunkRequestRecoversFromMissingOrInvalidTimestamp()
    {
        var chunk = new TerrainChunk(null!, 1, 2) { IsRequested = true };

        Assert.True(TerrainUpdater.ShouldRequestNetworkChunk(chunk, 20.0));

        chunk.NetworkRequestTime = 30.0;
        Assert.True(TerrainUpdater.ShouldRequestNetworkChunk(chunk, 20.0));
    }

    [Fact]
    public void NetworkChunkStallClassificationDistinguishesPipelineStages()
    {
        var chunk = new TerrainChunk(null!, 1, 2)
        {
            IsRequested = true,
            NetworkRequestTime = 10.0
        };

        Assert.True(TerrainUpdater.IsNetworkChunkStalled(chunk, 15.0));

        chunk.IsRequested = false;
        chunk.IsLoaded = true;
        chunk.NetworkContentReceiveTime = 20.0;
        chunk.NetworkContentVersion = 2;
        chunk.ClientGeometryContentVersion = 2;
        chunk.WorkerState = TerrainChunkState.Valid;
        chunk.MainThreadState = TerrainChunkState.Valid;
        Assert.True(TerrainUpdater.IsNetworkChunkStalled(chunk, 25.0));

        chunk.GeometryUploaded = true;
        Assert.False(TerrainUpdater.IsNetworkChunkStalled(chunk, 25.0));

        chunk.WorkerState = TerrainChunkState.InvalidPropagatedLight;
        chunk.MainThreadState = TerrainChunkState.InvalidPropagatedLight;
        Assert.False(TerrainUpdater.IsNetworkChunkStalled(chunk, 25.0));
    }

    [Fact]
    public void ClientRetentionMarginCreatesAllocationHysteresis()
    {
        var locations = new[]
        {
            new TerrainUpdater.UpdateLocation
            {
                Center = Vector2.Zero,
                VisibilityDistance = 128,
                ContentDistance = 128
            }
        };
        var bufferedCenter = new Vector2(152, 0);

        Assert.False(TerrainUpdater.IsChunkInRange(bufferedCenter, locations));
        Assert.True(TerrainUpdater.IsChunkInRange(
            bufferedCenter,
            locations,
            NetworkTerrainPolicy.ClientChunkRetentionMargin));
        Assert.False(TerrainUpdater.IsChunkInRange(
            new Vector2(161, 0),
            locations,
            NetworkTerrainPolicy.ClientChunkRetentionMargin));
    }

    [Fact]
    public void RetentionCapacityEvictsLeastRecentlyUsedChunks()
    {
        var oldest = new Point2(1, 0);
        var middle = new Point2(2, 0);
        var newest = new Point2(3, 0);

        var evicted = TerrainUpdater.SelectRetainedChunkCoordsToEvict([
            (middle, 20),
            (oldest, 10),
            (newest, 30)
        ], 2);

        Assert.Equal([oldest], evicted);
    }

    [Fact]
    public void ZeroRetentionCapacityEvictsEveryBufferedChunk()
    {
        var evicted = TerrainUpdater.SelectRetainedChunkCoordsToEvict([
            (new Point2(1, 0), 10),
            (new Point2(2, 0), 20)
        ], 0);

        Assert.Equal(2, evicted.Count);
    }
}
