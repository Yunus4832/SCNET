using Engine.Core;

using Game.Network;
using Game.Network.Serialization;
using Game.Terrains.Distribution;

namespace Survivalcraft.Test.Terrains.Distribution;

public sealed class ServerChunkDistributionSchedulerTest
{
    [Fact]
    public void UnreadyContentRetainsRequestsUntilInterestMovesAway()
    {
        using var scheduler = new ServerChunkDistributionScheduler(new EmptyAuthority(), 1);
        var client = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid(), null);
        var request = new ChunkContentRequest(new ChunkAllocationId(new Point2(0, 0), 1));
        scheduler.UpdateClientLocation(client, Vector2.Zero, 64f);
        scheduler.Enqueue(client, [request]);
        scheduler.EnqueueMissing(client, [new TerrainChunkFragmentRequest(request.Allocation, 1, 1, [0])]);

        scheduler.Update(true);

        Assert.Equal(2, scheduler.GetPendingCount(client));
        Assert.Equal((1, 1, 0), scheduler.GetBacklog());
        scheduler.UpdateClientLocation(client, new Vector2(1000f), 64f);
        Assert.Equal(0, scheduler.GetPendingCount(client));
    }

    [Fact]
    public void ContentCanStartEncodingBetweenSendWindows()
    {
        var authority = new CountingAuthority();
        using var scheduler = new ServerChunkDistributionScheduler(authority, 1);
        var client = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid(), null);
        scheduler.UpdateClientLocation(client, Vector2.Zero, 64f);
        scheduler.Enqueue(client, [new ChunkContentRequest(new ChunkAllocationId(new Point2(0, 0), 1))]);

        scheduler.Update(true);
        Assert.Equal(1, scheduler.GetPendingCount(client));
        Assert.Equal(0, authority.SnapshotAttempts);

        authority.Ready = true;
        scheduler.Update(false);

        Assert.Equal(1, scheduler.GetPendingCount(client));
        Assert.Equal(1, authority.SnapshotAttempts);
        Assert.Equal((1, 0, 0), scheduler.GetBacklog());
    }

    [Theory]
    [InlineData(2, 0, true)]
    [InlineData(2, 1, false)]
    [InlineData(-3, -1, true)]
    [InlineData(-3, -2, false)]
    public void RequestsAndQueueCleanupMatchSharedChunkInterest(int x, int z, bool expected)
    {
        using var scheduler = new ServerChunkDistributionScheduler(new EmptyAuthority(), 1);
        var client = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid(), null);
        var coords = new Point2(x, z);
        var request = new ChunkContentRequest(new ChunkAllocationId(coords, 1));
        var fragmentRequest = new TerrainChunkFragmentRequest(request.Allocation, 1, 1, [0]);
        Assert.Equal(0, scheduler.EnqueueMissing(client, [fragmentRequest]));
        var index = new SpatialInterestIndex();
        index.SetLocation(client, Vector2.Zero, 32f);
        scheduler.UpdateClientLocation(client, Vector2.Zero, 32f);

        Assert.Equal(expected, index.GetChunkObservers(coords).Contains(client));
        Assert.Equal(expected ? 1 : 0, scheduler.Enqueue(client, [request]));
        Assert.Equal(expected ? 1 : 0, scheduler.EnqueueMissing(client, [fragmentRequest]));
        Assert.Equal(expected ? 2 : 0, scheduler.GetPendingCount(client));
        var queue = new PendingChunkRequestQueue();
        queue.Enqueue(request);
        Assert.Equal(expected ? 0 : 1, queue.RemoveOutside(Vector2.Zero, 32f));

        scheduler.UpdateClientLocation(client, new Vector2(1000f), 32f);
        Assert.Equal(0, scheduler.GetPendingCount(client));
    }

    [Fact]
    public void ReusedConnectionIdentityKeepsIndependentQueuesAndDisconnectCleanup()
    {
        using var scheduler = new ServerChunkDistributionScheduler(new EmptyAuthority(), 1);
        var old = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid(), null);
        var current = new Client(null, old.ID, Guid.NewGuid(), old.GUID, null);
        var request = new ChunkContentRequest(new ChunkAllocationId(new Point2(0, 0), 1));
        scheduler.UpdateClientLocation(old, Vector2.Zero, 64f);
        scheduler.UpdateClientLocation(current, Vector2.Zero, 64f);

        Assert.Equal(1, scheduler.Enqueue(old, [request]));
        Assert.Equal(1, scheduler.Enqueue(current, [request]));
        Assert.Equal(2, scheduler.ClientCount);
        scheduler.RemoveClient(old);

        Assert.Equal(0, scheduler.GetPendingCount(old));
        Assert.Equal(1, scheduler.GetPendingCount(current));
        Assert.True(scheduler.TryGetClientLocation(current, out _, out _));
        Assert.Equal(1, scheduler.ClientCount);
    }

    [Fact]
    public void RequestsRequireApprovedLocationAndRejectDistantChunks()
    {
        using var scheduler = new ServerChunkDistributionScheduler(new EmptyAuthority(), 1);
        var client = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid(), null);
        var near = new ChunkContentRequest(new ChunkAllocationId(new Point2(0, 0), 1));
        var far = new ChunkContentRequest(new ChunkAllocationId(new Point2(100, 100), 1));

        Assert.Equal(0, scheduler.Enqueue(client, [near]));
        scheduler.UpdateClientLocation(client, Vector2.Zero, 64f);
        Assert.Equal(1, scheduler.Enqueue(client, [near, far]));
        Assert.Equal(1, scheduler.GetPendingCount(client));
    }

    [Fact]
    public void EmptyQueueRetainsApprovedLocationUntilDisconnect()
    {
        using var scheduler = new ServerChunkDistributionScheduler(new EmptyAuthority(), 1);
        var client = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid(), null);
        var request = new ChunkContentRequest(new ChunkAllocationId(new Point2(0, 0), 1));
        Assert.False(scheduler.TryGetClientLocation(client, out _, out _));
        scheduler.UpdateClientLocation(client, Vector2.Zero, 64f);
        Assert.Equal(0, scheduler.Enqueue(client, []));

        scheduler.Update();

        Assert.True(scheduler.TryGetClientLocation(client, out var center, out var distance));
        Assert.Equal(Vector2.Zero, center);
        Assert.Equal(64f, distance);
        Assert.Equal(1, scheduler.Enqueue(client, [request]));
        scheduler.RemoveClient(client);
        Assert.False(scheduler.TryGetClientLocation(client, out _, out _));
        Assert.Equal(0, scheduler.Enqueue(client, [request]));
    }

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
        var count = (ushort)EncodedTerrainChunkFragmenter.Split(encoded, allocation).Count();
        var request = new TerrainChunkFragmentRequest(allocation, 7, count, [1, 3, 3]);

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
        var count = (ushort)EncodedTerrainChunkFragmenter.Split(encoded, allocation).Count();
        var request = new TerrainChunkFragmentRequest(allocation, 7, count, [1]);

        Assert.False(ServerChunkDistributionScheduler.TrySelectMissingFragments(
            encoded,
            request,
            out var selected));
        Assert.Empty(selected);
    }

    private sealed class CountingAuthority : IChunkContentAuthority
    {
        public bool Ready { get; set; }

        public int SnapshotAttempts { get; private set; }

        public bool TryGetDescriptor(Point2 coords, out AuthorityChunkDescriptor descriptor)
        {
            descriptor = new AuthorityChunkDescriptor(coords, 1);
            return Ready;
        }

        public bool TryGetSnapshot(Point2 coords, out AuthorityChunkSnapshot snapshot)
        {
            SnapshotAttempts++;
            snapshot = null!;
            return false;
        }
    }

    private sealed class EmptyAuthority : IChunkContentAuthority
    {
        public bool TryGetDescriptor(Point2 coords, out AuthorityChunkDescriptor descriptor)
        {
            descriptor = default;
            return false;
        }

        public bool TryGetSnapshot(Point2 coords, out AuthorityChunkSnapshot snapshot)
        {
            snapshot = null!;
            return false;
        }
    }
}
