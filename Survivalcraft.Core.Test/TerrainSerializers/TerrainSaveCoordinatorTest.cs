using System.Collections.Concurrent;

using Game.Terrains;
using Game.TerrainSerializers;

namespace Survivalcraft.Core.Test.TerrainSerializers;

public sealed class TerrainSaveCoordinatorTest
{
    [Fact]
    public void CleanReservationProtectsCapacityAndCapturesChangesMadeByUnloadCallbacks()
    {
        var writes = new ConcurrentQueue<int>();
        using var coordinator = new TerrainSaveCoordinator((_, cells, _) => writes.Enqueue(cells[0]), 1);
        using var terrain = new Terrain();
        using var source = CreateDirtyChunk(terrain, 0, 0, 100);
        source.ModificationCounter = 0;
        using var nested = CreateDirtyChunk(terrain, 1, 0, 300);
        using var reservation = coordinator.TryReserveUnload(source);
        Assert.NotNull(reservation);
        Assert.False(coordinator.CanAcceptUnloadSnapshot);
        Assert.Null(coordinator.TryReserveUnload(source));
        Assert.False(coordinator.TryQueueChunkForUnload(nested));
        Assert.Equal(1, nested.ModificationCounter);

        source.Cells[0] = 200;
        source.ModificationCounter = 1;
        Assert.True(reservation.Commit(source));
        Assert.False(reservation.Commit(source));
        coordinator.Flush();
        Assert.Equal(new[] { 200 }, writes.ToArray());
        Assert.Equal(0, source.ModificationCounter);
        Assert.True(coordinator.CanAcceptUnloadSnapshot);
    }

    [Fact]
    public void DisposingAnUncommittedReservationReturnsItsSlotAndDoesNotClearDirtyData()
    {
        using var coordinator = new TerrainSaveCoordinator((_, _, _) => { }, 1);
        using var terrain = new Terrain();
        using var source = CreateDirtyChunk(terrain, 0, 0, 100);
        var reservation = coordinator.TryReserveUnload(source);
        Assert.NotNull(reservation);
        reservation.Dispose();
        reservation.Dispose();
        Assert.False(reservation.Commit(source));
        Assert.Equal(1, source.ModificationCounter);
        Assert.True(coordinator.CanAcceptUnloadSnapshot);
        using var again = coordinator.TryReserveUnload(source);
        Assert.NotNull(again);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("foreign")]
    [InlineData("renewed")]
    [InlineData("disposed")]
    public void InvalidCommitConsumesItsReservationAndCannotLeakCapacity(string failure)
    {
        using var coordinator = new TerrainSaveCoordinator((_, _, _) => { }, 1);
        using var terrain = new Terrain();
        var source = terrain.AllocateChunk(0, 0);
        source.HasContents = true;
        source.ModificationCounter = 1;
        using var foreign = CreateDirtyChunk(terrain, 0, 0, 200);
        using var reservation = coordinator.TryReserveUnload(source);
        Assert.NotNull(reservation);
        switch (failure)
        {
            case "null":
                Assert.Throws<ArgumentNullException>(() => reservation.Commit(null!));
                break;
            case "foreign":
                Assert.Throws<ArgumentException>(() => reservation.Commit(foreign));
                break;
            case "renewed":
                terrain.RenewChunkAllocation(source);
                Assert.Throws<ArgumentException>(() => reservation.Commit(source));
                break;
            case "disposed":
                source.Dispose();
                Assert.Throws<ObjectDisposedException>(() => reservation.Commit(source));
                break;
        }

        Assert.False(reservation.Commit(source));
        Assert.True(coordinator.CanAcceptUnloadSnapshot);
        Assert.Equal(1, source.ModificationCounter);
        Assert.Equal(0, coordinator.OutstandingCount);
    }

    [Fact]
    public void SnapshotCaptureExceptionReleasesCapacityAndPreservesDirtyData()
    {
        using var coordinator = new TerrainSaveCoordinator((_, _, _) => { }, 1);
        using var terrain = new Terrain();
        using var source = CreateDirtyChunk(terrain, 0, 0, 100);
        var cells = source.Cells;
        source.Cells = [];
        using var reservation = coordinator.TryReserveUnload(source);
        Assert.NotNull(reservation);
        Assert.Throws<ArgumentException>(() => reservation.Commit(source));
        Assert.True(coordinator.CanAcceptUnloadSnapshot);
        Assert.Equal(1, source.ModificationCounter);
        source.Cells = cells;
        Assert.True(coordinator.TryQueueChunkForUnload(source));
        coordinator.Flush();
    }

    [Fact]
    public void ClosingTheCoordinatorRevokesOutstandingReservationsWithoutSavingTheirDirtyChunks()
    {
        var calls = 0;
        var coordinator = new TerrainSaveCoordinator((_, _, _) => calls++, 1);
        using var terrain = new Terrain();
        using var source = CreateDirtyChunk(terrain, 0, 0, 100);
        using var reservation = coordinator.TryReserveUnload(source);
        Assert.NotNull(reservation);

        coordinator.Dispose();

        Assert.False(reservation.Commit(source));
        Assert.False(reservation.Commit(null!));
        Assert.Null(coordinator.TryReserveUnload(source));
        Assert.Equal(0, coordinator.OutstandingCount);
        Assert.Equal(0, calls);
        Assert.Equal(1, source.ModificationCounter);
        coordinator.Dispose();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UncommittedOrFailedReplacementKeepsThePreviousSnapshotRecoverable(bool captureFails)
    {
        var coordinator = new TerrainSaveCoordinator((_, _, _) => throw new IOException("Storage unavailable."), 1);
        using var terrain = new Terrain();
        using var source = CreateDirtyChunk(terrain, 0, 0, 100);
        using var replacement = CreateDirtyChunk(terrain, 0, 0, 200);
        Assert.True(coordinator.TryQueueChunkForUnload(source));
        Assert.Throws<AggregateException>(coordinator.Flush);
        using (var reservation = coordinator.TryReserveUnload(replacement))
        {
            Assert.NotNull(reservation);
            using var nested = CreateDirtyChunk(terrain, 1, 0, 300);
            Assert.Null(coordinator.TryReserveUnload(nested));
            if (captureFails)
            {
                replacement.Cells = [];
                Assert.Throws<ArgumentException>(() => reservation.Commit(replacement));
            }
        }

        using var restored = new TerrainChunk(terrain, 0, 0);
        Assert.True(coordinator.TryRestorePendingSnapshot(restored));
        Assert.Equal(100, restored.Cells[0]);
        Assert.Equal(1, restored.ModificationCounter);
        Assert.Equal(1, replacement.ModificationCounter);
        Assert.True(coordinator.CanAcceptUnloadSnapshot);
        coordinator.Dispose();
    }

    [Fact]
    public void FailedCloseStillRevokesItsReservationsAndLeavesUncommittedChunksDirty()
    {
        var coordinator = new TerrainSaveCoordinator((_, _, _) => throw new IOException("Storage unavailable."), 2);
        using var terrain = new Terrain();
        using var source = CreateDirtyChunk(terrain, 0, 0, 100);
        using var deferred = CreateDirtyChunk(terrain, 1, 0, 200);
        Assert.True(coordinator.TryQueueChunkForUnload(source));
        Assert.Throws<AggregateException>(coordinator.Flush);
        using var reservation = coordinator.TryReserveUnload(deferred);
        Assert.NotNull(reservation);

        Assert.Throws<AggregateException>(coordinator.Dispose);

        Assert.False(reservation.Commit(deferred));
        Assert.Equal(1, deferred.ModificationCounter);
        Assert.Equal(0, coordinator.OutstandingCount);
        coordinator.Dispose();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void FlushDoesNotStealAReplacementLeaseButCanRetryWhenAnotherSlotExists(int capacity)
    {
        var failing = 1;
        var writes = new ConcurrentQueue<int>();
        using var coordinator = new TerrainSaveCoordinator(
            (_, cells, _) =>
            {
                if (Volatile.Read(ref failing) != 0) { throw new IOException("Storage unavailable."); }

                writes.Enqueue(cells[0]);
            },
            capacity
        );
        using var terrain = new Terrain();
        using var source = CreateDirtyChunk(terrain, 0, 0, 100);
        using var replacement = CreateDirtyChunk(terrain, 0, 0, 200);
        Assert.True(coordinator.TryQueueChunkForUnload(source));
        Assert.Throws<AggregateException>(coordinator.Flush);
        using var reservation = coordinator.TryReserveUnload(replacement);
        Assert.NotNull(reservation);
        Volatile.Write(ref failing, 0);
        if (capacity == 1)
        {
            Assert.Throws<AggregateException>(coordinator.Flush);
            Assert.Empty(writes);
            Assert.False(coordinator.CanAcceptUnloadSnapshot);
        }
        else
        {
            coordinator.Flush();
            Assert.Equal(new[] { 100 }, writes.ToArray());
        }

        Assert.True(reservation.Commit(replacement));
        coordinator.Flush();
        Assert.Equal(capacity == 1 ? new[] { 200 } : new[] { 100, 200 }, writes.ToArray());
        Assert.True(coordinator.CanAcceptUnloadSnapshot);
    }

    [Fact]
    public void LoadedAuthorityChunkDoesNotRequireDerivedLightToBeSaved()
    {
        using var terrain = new Terrain();
        using var chunk = new TerrainChunk(terrain, 0, 0)
        {
            HasContents = true,

            ModificationCounter = 1
        };


        Assert.False(chunk.HasCurrentLight);
        Assert.True(TerrainSaveCoordinator.RequiresSave(chunk));
    }

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
            }
        );
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
            }
        );
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
        using var coordinator = new TerrainSaveCoordinator(
            (_, _, _) =>
            {
                writerStarted.Set();
                Assert.True(allowWriter.Wait(TimeSpan.FromSeconds(5)));
            },
            2
        );
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
            }
        );
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
            throw new IOException("Simulated storage failure.")
        );
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

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void FailedSnapshotsHoldCapacityUntilRestoredReplacedOrFlushed(bool replace, bool retryFlush)
    {
        var failing = 1;
        var writes = new ConcurrentQueue<int>();
        var coordinator = new TerrainSaveCoordinator(
            (_, cells, _) =>
            {
                if (Volatile.Read(ref failing) != 0) { throw new IOException("Storage unavailable."); }

                writes.Enqueue(cells[0]);
            },
            1
        );
        using var terrain = new Terrain();
        using var source = CreateDirtyChunk(terrain, 0, 0, 100);
        using var deferred = CreateDirtyChunk(terrain, 1, 0, 300);
        try
        {
            Assert.True(coordinator.TryQueueChunkForUnload(source));
            Assert.Throws<AggregateException>(coordinator.Flush);
            Assert.Equal(0, coordinator.OutstandingCount);
            Assert.False(coordinator.CanAcceptUnloadSnapshot);
            Assert.False(coordinator.TryQueueChunkForUnload(deferred));
            Assert.Equal(1, deferred.ModificationCounter);
            Volatile.Write(ref failing, 0);
            if (retryFlush) { coordinator.Flush(); }
            else if (replace)
            {
                using var replacement = CreateDirtyChunk(terrain, 0, 0, 200);
                Assert.True(coordinator.TryQueueChunkForUnload(replacement));
                coordinator.Flush();
            }
            else
            {
                using var restored = new TerrainChunk(terrain, 0, 0) { HasContents = true };

                Assert.True(coordinator.TryRestorePendingSnapshot(restored));
                Assert.Equal(100, restored.Cells[0]);
                Assert.Equal(1, restored.ModificationCounter);
                Assert.True(coordinator.TryQueueChunkForUnload(restored));
                coordinator.Flush();
            }

            Assert.True(coordinator.CanAcceptUnloadSnapshot);
            Assert.True(coordinator.TryQueueChunkForUnload(deferred));
            coordinator.Flush();
            Assert.Equal(new[] { replace ? 200 : 100, 300 }, writes.ToArray());
        }
        finally
        {
            Volatile.Write(ref failing, 0);
            try { coordinator.Flush(); }
            catch (AggregateException) { }

            for (var x = 0; x < 2; x++)
            {
                using var recover = new TerrainChunk(terrain, x, 0);
                coordinator.TryRestorePendingSnapshot(recover);
            }

            coordinator.Dispose();
        }
    }

    [Fact]
    public async Task DisposingStopsNewUnloadsBeforeWaitingForTheActiveWriter()
    {
        using var started = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var coordinator = new TerrainSaveCoordinator(
            (_, _, _) =>
            {
                started.Set();
                if (!release.Wait(TimeSpan.FromSeconds(10))) { throw new TimeoutException(); }
            },
            2
        );
        using var terrain = new Terrain();
        using var first = CreateDirtyChunk(terrain, 0, 0, 1);
        using var late = CreateDirtyChunk(terrain, 1, 0, 2);
        Task? disposing = null;
        try
        {
            Assert.True(coordinator.TryQueueChunkForUnload(first));
            Assert.True(started.Wait(TimeSpan.FromSeconds(5)));
            disposing = Task.Run(coordinator.Dispose);
            Assert.True(SpinWait.SpinUntil(() => !coordinator.CanAcceptUnloadSnapshot, TimeSpan.FromSeconds(5)));
            Assert.False(coordinator.TryQueueChunkForUnload(late));
            Assert.Equal(1, late.ModificationCounter);
        }
        finally
        {
            release.Set();
            if (disposing != null) { await disposing.WaitAsync(TimeSpan.FromSeconds(5)); }
        }
    }

    private static TerrainChunk CreateDirtyChunk(Terrain terrain, int x, int z, int value)
    {
        var chunk = new TerrainChunk(terrain, x, z)
        {
            HasContents = true,

            ModificationCounter = 1
        };

        chunk.Cells[0] = value;
        return chunk;
    }
}
