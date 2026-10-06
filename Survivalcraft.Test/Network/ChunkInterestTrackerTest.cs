using Engine.Core;

using Game.Network;

namespace Survivalcraft.Test.Network;

public sealed class ChunkInterestTrackerTest
{
    private readonly Client _client = new(null, 1, Guid.NewGuid(), Guid.NewGuid(), null);

    [Fact]
    public void EntryIsReportedOnceAndReentryRequiresANewBaseline()
    {
        var tracker = new ChunkInterestTracker();
        var chunk = new Point2(-2, 3);

        Assert.Equal([chunk], tracker.Synchronize(_client, [chunk]).Entered);
        var unchanged = tracker.Synchronize(_client, [chunk]);
        Assert.Empty(unchanged.Entered);
        Assert.Empty(unchanged.Left);
        var departed = tracker.Synchronize(_client, []);
        Assert.Empty(departed.Entered);
        Assert.Equal([chunk], departed.Left);
        Assert.Empty(tracker.Synchronize(_client, []).Left);
        var reentered = tracker.Synchronize(_client, [chunk]);
        Assert.Equal([chunk], reentered.Entered);
        Assert.Empty(reentered.Left);
    }

    [Fact]
    public void DisconnectedClientStateIsRemoved()
    {
        var tracker = new ChunkInterestTracker();
        var chunk = new Point2(1, 2);
        tracker.Synchronize(_client, [chunk]);

        tracker.RetainClients([]);

        Assert.Equal([chunk], tracker.Synchronize(_client, [chunk]).Entered);
    }

    [Fact]
    public void SynchronizationDoesNotRetainCallersMutableSet()
    {
        var tracker = new ChunkInterestTracker();
        var chunk = new Point2(1, 2);
        var current = new HashSet<Point2> { chunk };
        tracker.Synchronize(_client, current);

        current.Clear();

        Assert.Empty(tracker.Synchronize(_client, [chunk]).Entered);
    }

    [Fact]
    public void RepeatedUnchangedCoverageDoesNotAllocateAnotherFullChunkSet()
    {
        var tracker = new ChunkInterestTracker();
        var current = Enumerable.Range(0, 4096).Select(x => new Point2(x, -1)).ToHashSet();
        for (var i = 0; i < 10; i++)
        {
            tracker.Synchronize(_client, current);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 20; i++)
        {
            tracker.Synchronize(_client, current);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(allocated < 65536, $"Unchanged coverage allocated {allocated} bytes.");
    }

    [Fact]
    public void ReusedStorageKeepsCallerAndPreviousChangeSnapshotIndependent()
    {
        var tracker = new ChunkInterestTracker();
        var first = new Point2(-1, 0);
        var second = new Point2(0, 0);
        tracker.Synchronize(_client, [first]);
        var current = new HashSet<Point2> { second };
        var changes = tracker.Synchronize(_client, current);
        current.Clear();
        tracker.Synchronize(_client, [second]);

        Assert.Equal([second], changes.Entered);
        Assert.Equal([first], changes.Left);
        Assert.Empty(tracker.Synchronize(_client, [second]).Entered);
    }

    [Fact]
    public void ReusedClientNumberDoesNotShareConnectionState()
    {
        var tracker = new ChunkInterestTracker();
        var chunk = new Point2(1, 2);
        tracker.Synchronize(_client, [chunk]);
        var replacement = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid(), null);

        Assert.Equal([chunk], tracker.Synchronize(replacement, [chunk]).Entered);
    }
}
