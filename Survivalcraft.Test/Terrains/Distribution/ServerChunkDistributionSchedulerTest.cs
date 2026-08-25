using Engine.Core;

using Game.Network;
using Game.Network.Serialization;
using Game.Terrains.Distribution;

namespace Survivalcraft.Test.Terrains.Distribution;

public sealed class ServerChunkDistributionSchedulerTest
{
    [Fact]
    public void PredictedCenterPrioritizesChunksInMovementDirection()
    {
        var queue = new PendingChunkRequestQueue();
        queue.Enqueue(new ChunkContentRequest(new ChunkAllocationId(new Point2(-2, 0), 1)));
        queue.Enqueue(new ChunkContentRequest(new ChunkAllocationId(new Point2(2, 0), 1)));

        var selected = Assert.Single(queue.TakePrioritized(
            new Vector2(8, 8),
            new Vector2(40, 8),
            1));

        Assert.Equal(new Point2(2, 0), selected.Allocation.Coords);
    }

    [Fact]
    public void SelectiveRetransmissionReturnsOnlyRequestedFragments()
    {
        var allocation = new ChunkAllocationId(new Point2(4, 5), 6);
        var encoded = new EncodedTerrainChunk(allocation.Coords, 7, new byte[3200]);
        var request = new TerrainChunkFragmentRequest(allocation, 7, 4, [1, 3, 3]);

        Assert.True(ServerChunkDistributionScheduler.TrySelectMissingFragments(
            encoded,
            request,
            out var selected));

        Assert.Equal([1, 3], selected.Select(fragment => (int)fragment.FragmentIndex));
    }

    [Fact]
    public void SelectiveRetransmissionRejectsStaleContentVersion()
    {
        var allocation = new ChunkAllocationId(new Point2(4, 5), 6);
        var encoded = new EncodedTerrainChunk(allocation.Coords, 8, new byte[1800]);
        var request = new TerrainChunkFragmentRequest(allocation, 7, 2, [1]);

        Assert.False(ServerChunkDistributionScheduler.TrySelectMissingFragments(
            encoded,
            request,
            out var selected));
        Assert.Empty(selected);
    }
}
