using System.Collections.Concurrent;

using Game.Terrains;
using Game.TerrainSerializers;

namespace Survivalcraft.Core.Test.TerrainSerializers;

public sealed class TerrainFullSaveTest
{
    [Fact]
    public async Task ReservedBulkSlotCapturesWithoutWaitingForTheOccupiedUnloadWriter()
    {
        using var started = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var writes = new ConcurrentQueue<int>();
        using var coordinator = new TerrainSaveCoordinator(
            (_, cells, _) =>
            {
                started.Set();
                release.Wait();
                writes.Enqueue(cells[0]);
            },
            1
        );
        using var terrain = new Terrain();
        var first = Dirty(terrain, 0, 1);
        var second = Dirty(terrain, 1, 2);
        try
        {
            Assert.True(coordinator.TryQueueChunkForUnload(first));
            Assert.True(started.Wait(TimeSpan.FromSeconds(5)));
            var saved = coordinator.CaptureAndQueueSave(new[] { second });
            Assert.False(saved.IsCompleted);
            Assert.Equal(0, second.ModificationCounter);
            Assert.Throws<InvalidOperationException>(() =>
                {
                    _ = coordinator.CaptureAndQueueSave(Array.Empty<TerrainChunk>());
                }
            );
            second.Cells[0] = 99;
            second.ModificationCounter++;
            release.Set();
            await saved.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(new[] { 1, 2 }, writes.ToArray());
            Assert.Equal(1, second.ModificationCounter);
        }
        finally { release.Set(); }
    }

    [Fact]
    public async Task BulkMemoryBudgetDoesNotConsumeTheOrdinaryUnloadSlots()
    {
        using var started = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var coordinator = new TerrainSaveCoordinator(
            (_, _, _) =>
            {
                started.Set();
                release.Wait();
            },
            1
        );
        using var terrain = new Terrain();
        var chunks = Enumerable.Range(0, 5).Select(x => Dirty(terrain, x, x + 1)).ToArray();
        try
        {
            var full = coordinator.CaptureAndQueueSave(chunks);
            Assert.True(started.Wait(TimeSpan.FromSeconds(5)));
            Assert.True(coordinator.CanAcceptUnloadSnapshot);
            Assert.True(coordinator.TryQueueChunkForUnload(Dirty(terrain, 10, 10)));
            Assert.False(coordinator.CanAcceptUnloadSnapshot);
            release.Set();
            await full.WaitAsync(TimeSpan.FromSeconds(5));
            coordinator.Flush();
        }
        finally { release.Set(); }
    }

    [Fact]
    public async Task NewerUnloadCannotBeOverwrittenByTheOlderFullSave()
    {
        using var started = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var writes = new ConcurrentQueue<int>();
        using var coordinator = new TerrainSaveCoordinator((_, cells, _) =>
            {
                if (cells[0] == 10)
                {
                    started.Set();
                    release.Wait();
                }

                writes.Enqueue(cells[0]);
            }
        );
        using var terrain = new Terrain();
        var chunk = Dirty(terrain, 0, 10);
        try
        {
            var full = coordinator.CaptureAndQueueSave(new[] { chunk });
            Assert.True(started.Wait(TimeSpan.FromSeconds(5)));
            chunk.Cells[0] = 20;
            chunk.ModificationCounter++;
            Assert.True(coordinator.TryQueueChunkForUnload(chunk));
            release.Set();
            await full.WaitAsync(TimeSpan.FromSeconds(5));
            coordinator.Flush();
            Assert.Equal(new[] { 10, 20 }, writes.ToArray());
        }
        finally { release.Set(); }
    }

    [Fact]
    public async Task FailedBulkDataSurvivesDirtyResetAndCanBeRestoredAndRetried()
    {
        var fail = true;
        var writes = new ConcurrentQueue<int>();
        using var coordinator = new TerrainSaveCoordinator((_, cells, _) =>
            {
                if (fail) { throw new IOException("test failure"); }

                writes.Enqueue(cells[0]);
            }
        );
        using var terrain = new Terrain();
        var chunk = Dirty(terrain, 0, 7);
        await Assert.ThrowsAsync<AggregateException>(() => coordinator.CaptureAndQueueSave(new[] { chunk }));
        Assert.Equal(0, chunk.ModificationCounter);
        terrain.FreeChunk(chunk);
        var reloaded = terrain.AllocateChunk(0, 0);
        Assert.True(coordinator.TryRestorePendingSnapshot(reloaded));
        reloaded.HasContents = true;
        Assert.Equal(7, reloaded.Cells[0]);
        Assert.Equal(1, reloaded.ModificationCounter);
        fail = false;
        await coordinator.CaptureAndQueueSave(new[] { reloaded }).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(new[] { 7 }, writes.ToArray());
    }

    [Fact]
    public async Task EmptySaveFenceReportsAnEarlierUnloadFailureAndRetriesItOnTheNextSave()
    {
        using var started = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var fail = true;
        using var coordinator = new TerrainSaveCoordinator((_, _, _) =>
            {
                started.Set();
                release.Wait();
                if (fail) { throw new IOException("earlier unload failed"); }
            }
        );
        using var terrain = new Terrain();
        try
        {
            Assert.True(coordinator.TryQueueChunkForUnload(Dirty(terrain, 0, 8)));
            Assert.True(started.Wait(TimeSpan.FromSeconds(5)));
            var full = coordinator.CaptureAndQueueSave(Array.Empty<TerrainChunk>());
            release.Set();
            await Assert.ThrowsAsync<AggregateException>(() => full.WaitAsync(TimeSpan.FromSeconds(5)));
            fail = false;
            await coordinator.CaptureAndQueueSave(Array.Empty<TerrainChunk>()).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(0, coordinator.OutstandingCount);
        }
        finally { release.Set(); }
    }

    [Fact]
    public void CaptureFailureBeforeEnqueueDoesNotClearDirtyStateOrTakeOwnership()
    {
        using var coordinator = new TerrainSaveCoordinator((_, _, _) => { });
        using var terrain = new Terrain();
        var chunk = Dirty(terrain, 0, 9);

        IEnumerable<TerrainChunk> Broken()
        {
            yield return chunk;
            throw new InvalidOperationException("capture failure");
        }

        Assert.Throws<InvalidOperationException>(() => { _ = coordinator.CaptureAndQueueSave(Broken()); });
        Assert.Equal(1, chunk.ModificationCounter);
        Assert.Equal(9, chunk.Cells[0]);
        Assert.Equal(0, coordinator.OutstandingCount);
        Assert.True(coordinator.CanAcceptUnloadSnapshot);
    }

    private static TerrainChunk Dirty(Terrain terrain, int x, int value)
    {
        var chunk = terrain.AllocateChunk(x, 0);
        chunk.HasContents = true;
        chunk.Cells[0] = value;
        chunk.ModificationCounter = 1;
        return chunk;
    }
}
