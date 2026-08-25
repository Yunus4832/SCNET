using Engine.Core;

using Game.Network;
using Game.Terrains.Distribution;

namespace Survivalcraft.Test.Network;

public sealed class PendingChunkRequestQueueTest
{
    [Fact]
    public void DuplicateRequestsKeepOneEntryAndPreserveFirstRequestOrder()
    {
        var queue = new PendingChunkRequestQueue();

        var added = queue.EnqueueRange([
            Request(1, 2),
            Request(3, 4),
            Request(1, 2),
            Request(5, 6)
        ]);

        Assert.Equal(3, added);
        Assert.Equal(3, queue.Count);
        Assert.Equal([
            new Point2(1, 2), new Point2(3, 4), new Point2(5, 6)
        ], queue.Select(request => request.Allocation.Coords).ToArray());
    }

    [Fact]
    public void RemovedRequestCanBeQueuedAgainAtTheTail()
    {
        var queue = new PendingChunkRequestQueue();
        var first = new Point2(1, 2);
        var second = new Point2(3, 4);
        queue.EnqueueRange([Request(first.X, first.Y), Request(second.X, second.Y)]);

        Assert.True(queue.Remove(first));
        Assert.False(queue.Remove(first));
        Assert.True(queue.Enqueue(Request(first.X, first.Y)));

        Assert.Equal([second, first], queue.Select(request => request.Allocation.Coords).ToArray());
    }

    [Fact]
    public void NewAllocationReplacesQueuedRequestWithoutChangingOrder()
    {
        var queue = new PendingChunkRequestQueue();
        var first = Request(1, 2, 1);
        var replacement = Request(1, 2, 2);

        Assert.True(queue.Enqueue(first));
        Assert.False(queue.Enqueue(replacement));

        Assert.Equal(replacement, Assert.Single(queue));
    }

    [Fact]
    public void LocationUpdatePrunesOldRequestsAndPrioritizesNearest()
    {
        var queue = new PendingChunkRequestQueue();
        queue.EnqueueRange([Request(20, 20), Request(2, 0), Request(0, 0), Request(1, 0)]);

        var removed = queue.RemoveOutside(Vector2.Zero, 64f);
        var nearest = queue.TakeNearest(Vector2.Zero, 3).ToArray();

        Assert.Equal(1, removed);
        Assert.Equal([
            new Point2(0, 0), new Point2(1, 0), new Point2(2, 0)
        ], nearest.Select(request => request.Allocation.Coords).ToArray());
    }

    private static ChunkContentRequest Request(int x, int y, ulong generation = 1) =>
        new(new ChunkAllocationId(new Point2(x, y), generation));
}
