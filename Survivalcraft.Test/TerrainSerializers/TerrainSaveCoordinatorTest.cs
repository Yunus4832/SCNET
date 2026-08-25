using System.Collections.Concurrent;

using Game;
using Game.Terrains;
using Game.TerrainSerializers;

namespace Survivalcraft.Test.TerrainSerializers;

public sealed class TerrainSaveCoordinatorTest
{
    [Fact]
    public void QueuedSaveRunsInBackgroundAndFlushWaitsForCompletion()
    {
        using var writerStarted = new ManualResetEventSlim();
        using var allowWriter = new ManualResetEventSlim();
        var savedValue = 0;
        using var coordinator = new TerrainSaveCoordinator((_, cells, _) =>
        {
            writerStarted.Set();
            Assert.True(allowWriter.Wait(TimeSpan.FromSeconds(5)));
            savedValue = cells[TerrainChunk.CalculateCellIndex(1, 20, 3)];
        });
        using var terrain = new Terrain();
        using var chunk = CreateDirtyChunk(terrain, 0, 0, 71);
        chunk.SetCellValueFast(1, 20, 3, 2468);

        Assert.True(coordinator.TryQueueChunkForUnload(chunk));
        Assert.True(writerStarted.Wait(TimeSpan.FromSeconds(5)));
        Assert.Equal(0, chunk.ModificationCounter);
        Assert.Equal(1, coordinator.OutstandingCount);
        chunk.SetCellValueFast(1, 20, 3, 1357);

        allowWriter.Set();
        coordinator.Flush();

        Assert.Equal(2468, savedValue);
        Assert.Equal(0, coordinator.OutstandingCount);
    }

    [Fact]
    public void PendingSnapshotRestoresChunkBeforeDiskWriteCompletes()
    {
        using var writerStarted = new ManualResetEventSlim();
        using var allowWriter = new ManualResetEventSlim();
        using var coordinator = new TerrainSaveCoordinator((_, _, _) =>
        {
            writerStarted.Set();
            Assert.True(allowWriter.Wait(TimeSpan.FromSeconds(5)));
        });
        using var terrain = new Terrain();
        using var source = CreateDirtyChunk(terrain, 2, -3, 9753);

        Assert.True(coordinator.TryQueueChunkForUnload(source));
        Assert.True(writerStarted.Wait(TimeSpan.FromSeconds(5)));

        using var restored = new TerrainChunk(terrain, 2, -3);
        Assert.True(coordinator.TryRestorePendingSnapshot(restored));
        Assert.Equal(9753, restored.GetCellValueFast(0, 0, 0));
        Assert.Equal(1, restored.ModificationCounter);

        allowWriter.Set();
        coordinator.Flush();
        Assert.False(coordinator.TryRestorePendingSnapshot(restored));
    }

    [Fact]
    public void FullQueueDefersUnloadWithoutClearingDirtyState()
    {
        using var writerStarted = new ManualResetEventSlim();
        using var allowWriter = new ManualResetEventSlim();
        using var coordinator = new TerrainSaveCoordinator((_, _, _) =>
        {
            writerStarted.Set();
            Assert.True(allowWriter.Wait(TimeSpan.FromSeconds(5)));
        }, 2);
        using var terrain = new Terrain();
        using var first = CreateDirtyChunk(terrain, 0, 0, 1);
        using var second = CreateDirtyChunk(terrain, 1, 0, 2);
        using var deferred = CreateDirtyChunk(terrain, 2, 0, 3);

        Assert.True(coordinator.TryQueueChunkForUnload(first));
        Assert.True(writerStarted.Wait(TimeSpan.FromSeconds(5)));
        Assert.True(coordinator.TryQueueChunkForUnload(second));
        Assert.False(coordinator.TryQueueChunkForUnload(deferred));
        Assert.Equal(1, deferred.ModificationCounter);

        allowWriter.Set();
        coordinator.Flush();
    }

    [Fact]
    public void LatestSnapshotWinsWhenCoordinatesAreQueuedAgain()
    {
        using var writerStarted = new ManualResetEventSlim();
        using var allowWriter = new ManualResetEventSlim();
        var writes = new ConcurrentQueue<int>();
        using var coordinator = new TerrainSaveCoordinator((_, cells, _) =>
        {
            writerStarted.Set();
            Assert.True(allowWriter.Wait(TimeSpan.FromSeconds(5)));
            writes.Enqueue(cells[0]);
        });
        using var terrain = new Terrain();
        using var first = CreateDirtyChunk(terrain, 4, 5, 100);
        using var latest = CreateDirtyChunk(terrain, 4, 5, 200);

        Assert.True(coordinator.TryQueueChunkForUnload(first));
        Assert.True(writerStarted.Wait(TimeSpan.FromSeconds(5)));
        Assert.True(coordinator.TryQueueChunkForUnload(latest));

        using var restored = new TerrainChunk(terrain, 4, 5);
        Assert.True(coordinator.TryRestorePendingSnapshot(restored));
        Assert.Equal(200, restored.Cells[0]);

        allowWriter.Set();
        coordinator.Flush();
        Assert.Equal([100, 200], writes.ToArray());
    }

    [Fact]
    public void PermanentWriteFailureRemainsRecoverableAndFailsFlush()
    {
        var coordinator = new TerrainSaveCoordinator((_, _, _) =>
            throw new IOException("Simulated storage failure."));
        using var terrain = new Terrain();
        using var source = CreateDirtyChunk(terrain, -1, 7, 4321);

        Assert.True(coordinator.TryQueueChunkForUnload(source));
        Assert.Throws<AggregateException>(coordinator.Flush);

        using var restored = new TerrainChunk(terrain, -1, 7);
        Assert.True(coordinator.TryRestorePendingSnapshot(restored));
        Assert.Equal(4321, restored.Cells[0]);
        Assert.Equal(1, restored.ModificationCounter);
        coordinator.Dispose();
    }

    private static TerrainChunk CreateDirtyChunk(Terrain terrain, int x, int z, int value)
    {
        var chunk = new TerrainChunk(terrain, x, z)
        {
            MainThreadState = TerrainChunkState.Valid,
            ModificationCounter = 1
        };
        chunk.Cells[0] = value;
        return chunk;
    }
}
