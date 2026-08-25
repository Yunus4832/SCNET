using Engine.Core;

using Game.Terrains.Distribution;

namespace Survivalcraft.Test.Terrains.Distribution;

public sealed class ClientChunkContentStoreTest
{
    [Fact]
    public void ReallocationRejectsSnapshotFromPreviousLifetime()
    {
        var store = new ClientChunkContentStore();
        var first = store.Allocate(new Point2(3, 4));
        Assert.True(store.Release(first));
        var second = store.Allocate(first.Coords);

        Assert.NotEqual(first, second);
        Assert.False(store.TryInstall(CreateSnapshot(first, 1, 10)));
        Assert.True(store.TryInstall(CreateSnapshot(second, 1, 20)));
        Assert.True(store.TryGet(second, out var content));
        Assert.Equal(20, content.Cells.Span[0]);
    }

    [Fact]
    public void OlderAndDuplicateContentVersionsAreIdempotent()
    {
        var store = new ClientChunkContentStore();
        var allocation = store.Allocate(new Point2(1, 2));

        Assert.True(store.TryInstall(CreateSnapshot(allocation, 2, 20)));
        Assert.False(store.TryInstall(CreateSnapshot(allocation, 2, 21)));
        Assert.False(store.TryInstall(CreateSnapshot(allocation, 1, 10)));
        Assert.True(store.TryGet(allocation, out var content));
        Assert.Equal(2, content.ContentVersion);
        Assert.Equal(20, content.Cells.Span[0]);
    }

    private static ClientChunkSnapshot CreateSnapshot(
        ChunkAllocationId allocation,
        long version,
        int firstCell)
    {
        var cells = new int[AuthorityChunkSnapshot.CellCount];
        cells[0] = firstCell;
        return new ClientChunkSnapshot(
            allocation,
            version,
            cells,
            new long[AuthorityChunkSnapshot.ShaftCount]);
    }
}
