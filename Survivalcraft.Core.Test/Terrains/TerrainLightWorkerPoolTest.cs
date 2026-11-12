using System.Collections.Concurrent;

using Game.Terrains;
using Game.Terrains.Execution;
using Game.Terrains.Lighting;

namespace Survivalcraft.Core.Test.Terrains;

public sealed class TerrainLightWorkerPoolTest
{
    [Fact]
    public void CapturedComputationUsesImmutableInputsAfterItsAllocationRetires()
    {
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        chunk.HasContents = true;
        var transmission = new byte[65536];
        Array.Fill(transmission, (byte)1);
        var seed = new TerrainLightSeed(new TerrainLightSnapshot(new TerrainLightChannels(15, 7)), transmission);
        var job = new TerrainLightJob(chunk, terrain.Lighting.CaptureInput(chunk), seed, new TerrainLightSnapshot?[4]);
        Array.Fill(chunk.Cells, Terrain.MakeBlockValue(1));
        terrain.FreeChunk(chunk);
        var result = job.Compute();
        Assert.Equal(new TerrainLightChannels(15, 7), result.Get(8, 64, 8));
        Assert.False(terrain.Lighting.TryPublish(chunk, job.Input, result));
        using var computation = job.CreateComputation();
        Assert.True(computation.Advance(int.MaxValue));
        var independent = computation.Capture();
        computation.Dispose();
        Assert.Throws<ObjectDisposedException>(() => computation.Advance(1));
        Assert.Equal(new TerrainLightChannels(15, 7), independent.Get(8, 64, 8));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void AdmissionsRespectCapacityAndCompletionMessagesRetainTheirIdentity(int workers)
    {
        using var terrain = new Terrain();
        var job = CreateJob(terrain);
        using var entered = new CountdownEvent(workers);
        using var release = new ManualResetEventSlim();
        using var completed = new CountdownEvent(workers);
        var messages = new ConcurrentQueue<TerrainLightCompletion>();
        using var pool = new TerrainLightWorkerPool(
            workers,
            (_, cancellation) =>
            {
                entered.Signal();
                release.Wait(cancellation);
                return new TerrainLightSnapshot(new TerrainLightChannels(15, 7));
            },
            message =>
            {
                messages.Enqueue(message);
                completed.Signal();
            }
        );
        var tasks = new List<TerrainLightTask>();
        try
        {
            for (var i = 0; i < workers; i++)
            {
                Assert.True(pool.TrySchedule(job, out var task));
                tasks.Add(task);
            }

            Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
            Assert.False(pool.HasCapacity);
            Assert.False(pool.TrySchedule(job, out _));
            release.Set();
            Assert.True(completed.Wait(TimeSpan.FromSeconds(5)));
            Assert.True(pool.HasCapacity);
            Assert.Equal(workers, messages.Count);
            foreach (var message in messages)
            {
                Assert.Contains(message.Task, tasks);
                Assert.Null(message.Error);
                Assert.NotNull(message.Snapshot);
                Assert.Equal(new TerrainLightChannels(15, 7), message.Snapshot.Get(0, 0, 0));
            }
        }
        finally { release.Set(); }
    }

    [Fact]
    public void ClosingCancelsAndJoinsPrivateWorkEvenWhileWorldAccessIsHeld()
    {
        using var terrain = new Terrain();
        var job = CreateJob(terrain);
        using var entered = new ManualResetEventSlim();
        using var access = new TerrainAccessGate();
        TerrainLightCompletion? result = null;
        using var pool = new TerrainLightWorkerPool(
            1,
            (_, cancellation) =>
            {
                Assert.False(access.IsHeldByCurrentThread);
                entered.Set();
                cancellation.WaitHandle.WaitOne();
                cancellation.ThrowIfCancellationRequested();
                return new TerrainLightSnapshot(default);
            },
            message => result = message
        );
        access.Enter();
        try
        {
            Assert.True(pool.TrySchedule(job, out var task));
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
            pool.Dispose();
            Assert.True(task.CancellationRequested);
            Assert.True(task.Worker.IsCompleted);
            Assert.False(pool.TrySchedule(job, out _));
            Assert.True(result.HasValue);
            Assert.Null(result.Value.Error);
            Assert.Null(result.Value.Snapshot);
        }
        finally { access.Exit(); }
    }

    [Fact]
    public async Task ClosingJoinsCompletionNotificationsAfterCalculationCapacityIsReleased()
    {
        using var terrain = new Terrain();
        var job = CreateJob(terrain);
        using var notified = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var closingStarted = new ManualResetEventSlim();
        using var pool = new TerrainLightWorkerPool(
            1,
            static (_, _) => new TerrainLightSnapshot(default),
            _ =>
            {
                notified.Set();
                release.Wait();
            }
        );
        Task? closing = null;
        try
        {
            Assert.True(pool.TrySchedule(job, out var task));
            Assert.True(notified.Wait(TimeSpan.FromSeconds(5)));
            Assert.True(pool.HasCapacity);
            closing = Task.Run(() =>
                {
                    closingStarted.Set();
                    pool.Dispose();
                }
            );
            Assert.True(closingStarted.Wait(TimeSpan.FromSeconds(5)));
            Assert.False(closing.Wait(TimeSpan.FromMilliseconds(100)));
            release.Set();
            await closing.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(task.Worker.IsCompleted);
            Assert.False(pool.HasCapacity);
        }
        finally
        {
            release.Set();
            if (closing != null) { await closing.WaitAsync(TimeSpan.FromSeconds(5)); }
        }
    }

    [Fact]
    public void ACalculationFaultIsDeliveredAndCapacityCanBeUsedAgain()
    {
        using var terrain = new Terrain();
        var job = CreateJob(terrain);
        using var completed = new AutoResetEvent(false);
        var messages = new ConcurrentQueue<TerrainLightCompletion>();
        var calls = 0;
        using var pool = new TerrainLightWorkerPool(
            1,
            (_, _) => Interlocked.Increment(ref calls) == 1
                ? throw new InvalidOperationException("fixture calculation failure")
                : new TerrainLightSnapshot(default),
            message =>
            {
                messages.Enqueue(message);
                completed.Set();
            }
        );
        Assert.True(pool.TrySchedule(job, out _));
        Assert.True(completed.WaitOne(TimeSpan.FromSeconds(5)));
        Assert.True(messages.TryDequeue(out var failed));
        Assert.IsType<InvalidOperationException>(failed.Error);
        Assert.Null(failed.Snapshot);
        Assert.True(pool.TrySchedule(job, out _));
        Assert.True(completed.WaitOne(TimeSpan.FromSeconds(5)));
        Assert.True(messages.TryDequeue(out var recovered));
        Assert.Null(recovered.Error);
        Assert.NotNull(recovered.Snapshot);
    }

    [Fact]
    public async Task ACompletedHandleCanStillBeCancelledBeforeTheOwnerPublishes()
    {
        using var terrain = new Terrain();
        var job = CreateJob(terrain);
        using var completed = new ManualResetEventSlim();
        using var pool = new TerrainLightWorkerPool(
            1,
            static (input, cancellation) => input.Compute(cancellation),
            _ => completed.Set()
        );
        Assert.True(pool.TrySchedule(job, out var task));
        Assert.True(completed.Wait(TimeSpan.FromSeconds(5)));
        await task.Worker.WaitAsync(TimeSpan.FromSeconds(5));
        task.Cancel();
        Assert.True(task.CancellationRequested);
    }

    private static TerrainLightJob CreateJob(Terrain terrain)
    {
        var chunk = terrain.AllocateChunk(0, 0);
        chunk.HasContents = true;
        var transmission = new byte[65536];
        Array.Fill(transmission, (byte)1);
        return new TerrainLightJob(
            chunk,
            terrain.Lighting.CaptureInput(chunk),
            new TerrainLightSeed(new TerrainLightSnapshot(default), transmission),
            new TerrainLightSnapshot?[4]
        );
    }
}
