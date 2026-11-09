using Game.Terrains.Execution;

namespace Survivalcraft.Core.Test.Terrains.Execution;

public sealed class TerrainAccessGateTest
{
    [Fact]
    public void ReentrantOwnerCannotReleaseAnotherOwnersAccess()
    {
        using var gate = new TerrainAccessGate();
        gate.Enter();
        gate.Enter();
        Assert.True(gate.IsHeldByCurrentThread);
        Assert.False(TryFromAnotherThread(gate));
        gate.Exit();
        Assert.False(TryFromAnotherThread(gate));
        Exception? wrongOwner = null;
        var wrongThread = new Thread(() => wrongOwner = Record.Exception(gate.Exit)) { IsBackground = true };
        wrongThread.Start();
        Assert.True(wrongThread.Join(TimeSpan.FromSeconds(5)));
        Assert.IsType<SynchronizationLockException>(wrongOwner);
        gate.Exit();
        Assert.True(TryFromAnotherThread(gate));
    }

    [Fact]
    public void ClosingCancelsABlockedWorkerWithoutRequiringTheMainOwnerToReleaseFirst()
    {
        using var gate = new TerrainAccessGate();
        gate.Enter();
        using var attempting = new ManualResetEventSlim();
        using var finished = new ManualResetEventSlim();
        var acquired = true;
        var worker = new Thread(() =>
            {
                attempting.Set();
                acquired = gate.TryEnter(Timeout.Infinite);
                if (acquired) { gate.Exit(); }

                finished.Set();
            }
        );
        worker.Start();
        Assert.True(attempting.Wait(TimeSpan.FromSeconds(5)));
        gate.Close();
        Assert.True(finished.Wait(TimeSpan.FromSeconds(5)));
        Assert.True(worker.Join(TimeSpan.FromSeconds(5)));
        Assert.False(acquired);
        Assert.True(gate.IsHeldByCurrentThread);
        gate.Exit();
        Assert.False(gate.TryEnter());
        Assert.Throws<ObjectDisposedException>(gate.Enter);
    }

    [Fact]
    public void ClosingInsideANestedLeaseStillAllowsTheOwnerToUnwind()
    {
        var gate = new TerrainAccessGate();
        gate.Enter();
        gate.Enter();
        gate.Dispose();
        gate.Exit();
        Assert.True(gate.IsHeldByCurrentThread);
        gate.Exit();
        gate.Dispose();
        Assert.False(gate.TryEnter());
    }

    private static bool TryFromAnotherThread(TerrainAccessGate gate)
    {
        var acquired = false;
        Exception? failure = null;
        var worker = new Thread(() =>
            {
                failure = Record.Exception(() =>
                    {
                        acquired = gate.TryEnter();
                        if (acquired) { gate.Exit(); }
                    }
                );
            }
        ) { IsBackground = true };
        worker.Start();
        Assert.True(worker.Join(TimeSpan.FromSeconds(5)));
        Assert.Null(failure);
        return acquired;
    }
}
