using System.Collections.Concurrent;

using Engine.Core.Logging;

using Game.Terrains.Execution;

namespace Survivalcraft.Core.Test.Terrains.Execution;

public sealed class TerrainWorkExecutorTest
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ClosingLifetimeSuppressesOnlyExpectedCancellation(bool realFailure)
    {
        using var lifetime = new CancellationTokenSource();
        using var started = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var sink = new StopLogSink();
        using var executor = new TerrainWorkExecutor(() =>
            {
                started.Set();
                release.Wait();
                if (realFailure) { throw new InvalidOperationException("terrain-stop-test-probe"); }

                throw new AggregateException(new OperationCanceledException("terrain-stop-test-probe", lifetime.Token));
            }
        );
        Log.AddLogSink(sink);
        try
        {
            executor.Start(lifetime.Token);
            executor.Wake();
            Assert.True(started.Wait(TimeSpan.FromSeconds(5)));
            lifetime.Cancel();
            release.Set();
            await Task.Run(executor.Dispose).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(
                realFailure,
                sink.Errors.Any(message => message.Contains("terrain-stop-test-probe", StringComparison.Ordinal))
            );
        }
        finally
        {
            release.Set();
            executor.Dispose();
            Log.RemoveLogSink(sink);
        }
    }

    private sealed class StopLogSink : ILogSink
    {
        public ConcurrentQueue<string> Errors { get; } = new();

        public void Log(LogType type, string message)
        {
            if (type == LogType.Error) { Errors.Enqueue(message); }
        }
    }

    [Fact]
    public void RequestHasAccessDescribesOnlyTheCurrentThreadsAcquiredLease()
    {
        using var executor = new TerrainWorkExecutor(() => true);
        using var request = executor.CreateMainRequest();
        Assert.False(request.HasAccess);
        Assert.True(request.TryEnter());
        Assert.True(request.HasAccess);
        var ownerThread = Environment.CurrentManagedThreadId;
        var otherThread = ownerThread;
        var otherHasAccess = true;
        var reader = new Thread(() =>
            {
                otherThread = Environment.CurrentManagedThreadId;
                otherHasAccess = request.HasAccess;
            }
        );
        reader.Start();
        Assert.True(reader.Join(TimeSpan.FromSeconds(5)));
        Assert.NotEqual(ownerThread, otherThread);
        Assert.False(otherHasAccess);
        request.YieldAccess();
        Assert.False(request.HasAccess);
        Assert.True(request.IsPending);
        Assert.True(request.TryEnter());
        executor.Dispose();
        Assert.True(request.HasAccess);
        request.Leave();
        Assert.False(request.HasAccess);
        Assert.False(request.IsPending);
    }

    [Fact]
    public async Task YieldedForegroundPauseAllowsOtherThreadAccessWithoutResumingWorker()
    {
        using var first = new ManualResetEventSlim();
        using var finishFirst = new ManualResetEventSlim();
        using var resumed = new ManualResetEventSlim();
        var count = 0;
        using var executor = new TerrainWorkExecutor(() =>
            {
                if (Interlocked.Increment(ref count) == 1)
                {
                    first.Set();
                    Assert.True(finishFirst.Wait(TimeSpan.FromSeconds(5)));
                    return false;
                }

                resumed.Set();
                return true;
            }
        );
        using var batch = executor.CreateMainRequest();
        executor.Start();
        executor.Wake();
        Assert.True(first.Wait(TimeSpan.FromSeconds(5)));
        Assert.False(batch.TryEnter());
        finishFirst.Set();
        Assert.True(batch.TryEnter(5000));
        batch.YieldAccess();
        Assert.True(batch.IsPending);
        Assert.False(executor.Access.IsHeldByCurrentThread);

        await Task.Run(() =>
            {
                Assert.True(executor.TryEnterMain(5000));
                try { Assert.Equal(1, Volatile.Read(ref count)); }
                finally { executor.LeaveMain(); }
            }
        );
        Assert.True(batch.IsPending);
        Assert.False(resumed.IsSet);
        batch.Dispose();
        Assert.True(resumed.Wait(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void YieldRejectsNestedLeasesAndDoesNotReleaseAnotherRequestsAccess()
    {
        using var executor = new TerrainWorkExecutor(() => true);
        using var outer = executor.CreateMainRequest();
        using var batch = executor.CreateMainRequest();
        Assert.True(outer.TryEnter());
        Assert.True(batch.TryEnter());
        Assert.True(batch.TryEnter());
        Assert.Throws<SynchronizationLockException>(batch.YieldAccess);
        batch.Leave();
        batch.YieldAccess();
        Assert.True(executor.Access.IsHeldByCurrentThread);
        Assert.True(outer.IsPending);
        Assert.True(batch.IsPending);
        batch.Dispose();
        Assert.True(executor.HasMainRequest);
        outer.Leave();
        Assert.False(executor.HasMainRequest);
        Assert.False(executor.Access.IsHeldByCurrentThread);
    }

    [Fact]
    public void ClosingWhileHoldingARequestStillAllowsItsAccessToUnwind()
    {
        using var executor = new TerrainWorkExecutor(() => true);
        using var request = executor.CreateMainRequest();
        Assert.True(request.TryEnter());
        executor.Dispose();
        request.YieldAccess();
        request.Dispose();
        Assert.False(request.IsPending);
        Assert.False(executor.Access.IsHeldByCurrentThread);
        Assert.False(executor.HasMainRequest);
    }

    [Fact]
    public void CompletingLocationWorkCannotConsumeAnotherPendingInstallation()
    {
        using var executor = new TerrainWorkExecutor(() => true);
        using var content = executor.CreateMainRequest();
        using var locations = executor.CreateMainRequest();
        Assert.True(content.TryEnter());
        Assert.True(locations.TryEnter());
        locations.Leave();
        Assert.False(locations.IsPending);
        Assert.True(content.IsPending);
        Assert.True(executor.HasMainRequest);
        content.Leave();
        Assert.False(executor.HasMainRequest);
    }

    [Fact]
    public void CancellingAnUnacquiredRequestDoesNotLeaveTheWorkerPaused()
    {
        using var entered = new ManualResetEventSlim();
        using var complete = new ManualResetEventSlim();
        using var resumed = new ManualResetEventSlim();
        var count = 0;
        using var executor = new TerrainWorkExecutor(() =>
            {
                if (Interlocked.Increment(ref count) == 1)
                {
                    entered.Set();
                    Assert.True(complete.Wait(TimeSpan.FromSeconds(5)));
                    return false;
                }

                resumed.Set();
                return true;
            }
        );
        executor.Start();
        executor.Wake();
        Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
        using var content = executor.CreateMainRequest();
        Assert.False(content.TryEnter());
        content.Dispose();
        Assert.False(executor.HasMainRequest);
        complete.Set();
        Assert.True(resumed.Wait(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void RepeatedMeshWakeupsCannotCancelAPendingContentHandoff()
    {
        using var firstStep = new ManualResetEventSlim();
        using var completeFirst = new ManualResetEventSlim();
        using var secondStep = new ManualResetEventSlim();
        var count = 0;
        using var executor = new TerrainWorkExecutor(() =>
            {
                if (Interlocked.Increment(ref count) == 1)
                {
                    firstStep.Set();
                    Assert.True(completeFirst.Wait(TimeSpan.FromSeconds(5)));
                    return false;
                }

                secondStep.Set();
                return true;
            }
        );
        using var handoff = executor.CreateMainRequest();
        executor.Start();
        executor.Wake();
        Assert.True(firstStep.Wait(TimeSpan.FromSeconds(5)));
        Assert.False(handoff.TryEnter());
        for (var i = 0; i < 10; i++) { executor.Wake(); }

        completeFirst.Set();
        Assert.True(SpinWait.SpinUntil(() => handoff.TryEnter(), TimeSpan.FromSeconds(5)));
        Assert.True(executor.HasMainRequest);
        Assert.Equal(1, Volatile.Read(ref count));
        executor.EnterMain();
        executor.LeaveMain();
        Assert.True(executor.HasMainRequest);
        handoff.Leave();
        Assert.True(secondStep.Wait(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void NestedMainCallsKeepTheWorkerPausedUntilTheOuterCallFinishes()
    {
        using var stepped = new ManualResetEventSlim();
        using var executor = new TerrainWorkExecutor(() =>
            {
                stepped.Set();
                return true;
            }
        );
        executor.EnterMain();
        executor.Start();
        executor.Wake();
        executor.EnterMain();
        executor.LeaveMain();
        Assert.True(executor.HasMainRequest);
        Assert.False(stepped.IsSet);
        executor.LeaveMain();
        Assert.True(stepped.Wait(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void ClosingDuringAMainCallStopsTheWorkerAndTheLeaseCanStillBeReleased()
    {
        var steps = 0;
        var executor = new TerrainWorkExecutor(() =>
            {
                Interlocked.Increment(ref steps);
                return true;
            }
        );
        executor.EnterMain();
        executor.Start();
        executor.Wake();
        executor.Dispose();
        Assert.Equal(0, Volatile.Read(ref steps));
        executor.LeaveMain();
        Assert.False(executor.TryEnterMain());
        executor.Dispose();
    }

    [Fact]
    public void ConstructorDoesNotStartTheWorkerBeforeCompositionIsComplete()
    {
        var steps = 0;
        using var executor = new TerrainWorkExecutor(() =>
            {
                steps++;
                return true;
            }
        );
        executor.Wake();
        Assert.Equal(0, steps);
        executor.EnterMain();
        executor.LeaveMain();
    }
}
