using Engine.Core;
using Game.Terrains.Distribution;

namespace Survivalcraft.Test.Terrains.Distribution;

public sealed class InProcessChunkContentTransportTest
{
    [Fact]
    public void LocalTransportPreservesAllocationAndCopiesAuthorityContents()
    {
        var cells = new int[AuthorityChunkSnapshot.CellCount];
        cells[0] = 42;
        var authority = new FakeAuthority(new AuthorityChunkSnapshot(
            new Point2(5, 6),
            3,
            cells,
            new long[AuthorityChunkSnapshot.ShaftCount]));
        var transport = new InProcessChunkContentTransport(authority);
        var allocation = new ChunkAllocationId(new Point2(5, 6), 9);

        transport.Request([new ChunkContentRequest(allocation)]);
        cells[0] = 99;
        var received = new List<ClientChunkSnapshot>();

        Assert.Equal(1, transport.DrainReceived(received));
        Assert.Equal(allocation, received[0].Allocation);
        Assert.Equal(3, received[0].ContentVersion);
        Assert.Equal(42, received[0].Cells.Span[0]);
    }

    [Fact]
    public void LocalTransportDoesNotPublishKnownVersion()
    {
        var coords = new Point2(5, 6);
        var authority = new FakeAuthority(new AuthorityChunkSnapshot(
            coords,
            3,
            new int[AuthorityChunkSnapshot.CellCount],
            new long[AuthorityChunkSnapshot.ShaftCount]));
        var transport = new InProcessChunkContentTransport(authority);

        transport.Request([new ChunkContentRequest(new ChunkAllocationId(coords, 1), 3)]);

        Assert.Equal(0, transport.DrainReceived(new List<ClientChunkSnapshot>()));
    }

    [Fact]
    public void NewRequestSupersedesQueuedSnapshotForSameCoordinates()
    {
        var coords = new Point2(5, 6);
        var authority = new FakeAuthority(new AuthorityChunkSnapshot(
            coords,
            3,
            new int[AuthorityChunkSnapshot.CellCount],
            new long[AuthorityChunkSnapshot.ShaftCount]));
        var transport = new InProcessChunkContentTransport(authority);

        transport.Request([new ChunkContentRequest(new ChunkAllocationId(coords, 1))]);
        transport.Request([new ChunkContentRequest(new ChunkAllocationId(coords, 2))]);
        var received = new List<ClientChunkSnapshot>();

        Assert.Equal(1, transport.DrainReceived(received));
        Assert.Equal(2UL, received[0].Allocation.Generation);
    }

    [Fact]
    public void DiscardRemovesQueuedSnapshot()
    {
        var coords = new Point2(5, 6);
        var authority = new FakeAuthority(new AuthorityChunkSnapshot(
            coords,
            3,
            new int[AuthorityChunkSnapshot.CellCount],
            new long[AuthorityChunkSnapshot.ShaftCount]));
        var transport = new InProcessChunkContentTransport(authority);
        transport.Request([new ChunkContentRequest(new ChunkAllocationId(coords, 1))]);

        transport.Discard(coords);

        Assert.Equal(0, transport.DrainReceived(new List<ClientChunkSnapshot>()));
    }

    private sealed class FakeAuthority(AuthorityChunkSnapshot snapshot) : IChunkContentAuthority
    {
        public bool TryGetDescriptor(Point2 coords, out AuthorityChunkDescriptor descriptor)
        {
            descriptor = new AuthorityChunkDescriptor(snapshot.Coords, snapshot.ContentVersion);
            return snapshot.Coords == coords;
        }

        public bool TryGetSnapshot(Point2 coords, out AuthorityChunkSnapshot result)
        {
            result = snapshot;
            return snapshot.Coords == coords;
        }
    }
}
