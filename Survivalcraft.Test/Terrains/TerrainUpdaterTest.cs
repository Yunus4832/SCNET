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
    public void QueuedRemovalDoesNotReuseAnAppliedTeleportLocation()
    {
        var applied = new TerrainUpdater.UpdateLocation
        {
            Center = new Vector2(100f, 100f),
            ContentDistance = 16f,
            LastChunksUpdateCenter = new Vector2(100f, 100f)
        };
        var current = new Dictionary<int, TerrainUpdater.UpdateLocation> { [-2] = applied };
        var pending = new Dictionary<int, TerrainUpdater.UpdateLocation?> { [-2] = null };
        Assert.Equal(default, TerrainUpdater.ResolveUpdateLocation(-2, current, pending));
        pending.Clear();
        Assert.Equal(applied, TerrainUpdater.ResolveUpdateLocation(-2, current, pending));
    }

    [Fact]
    public void LatestQueuedLocationWinsBeforeWorkerHandoff()
    {
        var latest = new TerrainUpdater.UpdateLocation { Center = new Vector2(200f, 200f) };
        var current = new Dictionary<int, TerrainUpdater.UpdateLocation> { [-2] = new() { Center = Vector2.Zero } };
        var pending = new Dictionary<int, TerrainUpdater.UpdateLocation?> { [-2] = latest };
        Assert.Equal(latest, TerrainUpdater.ResolveUpdateLocation(-2, current, pending));
    }

    [Theory]
    [InlineData(-1, -1)]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(0, 0)]
    public void ContentOnlyTeleportLocationSchedulesAllChunksAtABoundary(int x, int z)
    {
        using var chunk = new TerrainChunk(null!, x, z);
        var locations = new Dictionary<int, TerrainUpdater.UpdateLocation>
        {
            [-2] = new() { Center = Vector2.Zero, VisibilityDistance = 0f, ContentDistance = 16f }
        };
        var selected = TerrainUpdater.SelectChunkToUpdate(
            [chunk], locations.Values, TerrainContentRole.Authority, true, out var state);
        Assert.Same(chunk, selected);
        Assert.Equal(TerrainChunkState.InvalidVertices1, state);
    }

    [Theory]
    [InlineData(TerrainContentRole.Authority, true, false, true)]
    [InlineData(TerrainContentRole.Authority, true, true, true)]
    [InlineData(TerrainContentRole.Authority, false, false, false)]
    [InlineData(TerrainContentRole.Replica, true, false, false)]
    public void ContentPriorityPreservesNormalAndReplicaScheduling(
        TerrainContentRole role,
        bool preferContent,
        bool reverse,
        bool selectsContent)
    {
        using var derived = new TerrainChunk(null!, 0, 0) { WorkerState = TerrainChunkState.InvalidLight };
        using var content = new TerrainChunk(null!, 2, 0);
        TerrainChunk[] chunks = reverse ? [content, derived] : [derived, content];
        var locations = new Dictionary<int, TerrainUpdater.UpdateLocation>
        {
            [0] = new() { Center = derived.Center, VisibilityDistance = 64f, ContentDistance = 64f }
        };

        var selected = TerrainUpdater.SelectChunkToUpdate(chunks, locations.Values, role, preferContent, out _);

        Assert.Same(selectsContent ? content : derived, selected);
    }

    [Fact]
    public void ContentPriorityStillChoosesNearestRelevantContent()
    {
        using var near = new TerrainChunk(null!, 1, 0);
        using var far = new TerrainChunk(null!, 2, 0);
        using var outside = new TerrainChunk(null!, 10, 0);
        var locations = new Dictionary<int, TerrainUpdater.UpdateLocation>
        {
            [0] = new() { Center = new Vector2(8f, 8f), VisibilityDistance = 32f, ContentDistance = 64f }
        };

        var selected = TerrainUpdater.SelectChunkToUpdate(
            [far, outside, near], locations.Values, TerrainContentRole.Authority, true, out var state);

        Assert.Same(near, selected);
        Assert.Equal(TerrainChunkState.Valid, state);
    }

    [Fact]
    public void MissingRelevantContentFallsBackToDerivedWork()
    {
        using var derived = new TerrainChunk(null!, 0, 0) { WorkerState = TerrainChunkState.InvalidLight };
        using var outside = new TerrainChunk(null!, 10, 0);
        var locations = new Dictionary<int, TerrainUpdater.UpdateLocation>
        {
            [0] = new() { Center = derived.Center, VisibilityDistance = 64f, ContentDistance = 64f }
        };

        var selected = TerrainUpdater.SelectChunkToUpdate(
            [outside, derived], locations.Values, TerrainContentRole.Authority, true, out _);

        Assert.Same(derived, selected);
    }

    [Fact]
    public void LocationUpdateTakesExactlyOneWorkerToken()
    {
        using var pause = new ManualResetEvent(true);
        using var token = new AutoResetEvent(true);

        Assert.True(TerrainUpdater.TryPauseUpdateThread(pause, token));
        Assert.False(pause.WaitOne(0));
        Assert.False(token.WaitOne(0));

        // Only the owner releases the token after modifying terrain.
        pause.Set();
        Assert.False(token.WaitOne(0));
        token.Set();
        Assert.True(token.WaitOne(0));
        Assert.False(token.WaitOne(0));
    }

    [Fact]
    public void UnfinishedWorkerStepDefersLocationUpdateWithoutLosingItsToken()
    {
        using var pause = new ManualResetEvent(true);
        using var token = new AutoResetEvent(false);

        Assert.False(TerrainUpdater.TryPauseUpdateThread(pause, token, 0));
        Assert.False(pause.WaitOne(0));

        // The active step completes, leaving the worker paused for the next attempt.
        token.Set();
        Assert.True(TerrainUpdater.TryPauseUpdateThread(pause, token, 0));
        Assert.False(token.WaitOne(0));
    }

    [Fact]
    public async Task LocationUpdateCanAcceptTheCurrentStepCompletionWithoutAnotherFrame()
    {
        using var pause = new ManualResetEvent(true);
        using var token = new AutoResetEvent(false);
        var worker = Task.Run(() =>
        {
            Assert.True(SpinWait.SpinUntil(() => !pause.WaitOne(0), TimeSpan.FromSeconds(5)));
            token.Set();
        });

        // A generous test deadline validates the handoff rather than OS timer precision.
        Assert.True(TerrainUpdater.TryPauseUpdateThread(pause, token, 5000));
        await worker;
        Assert.False(pause.WaitOne(0));
        Assert.False(token.WaitOne(0));
    }

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
