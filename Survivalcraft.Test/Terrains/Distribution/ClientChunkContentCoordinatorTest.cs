using Engine.Core;
using Game.Terrains;
using Game.Terrains.Distribution;

namespace Survivalcraft.Test.Terrains.Distribution;

public sealed class ClientChunkContentCoordinatorTest
{
    [Fact]
    public void InstallsOnlyCurrentAllocationAndNewerContentVersion()
    {
        var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(1, 2);
        var coordinator = new ClientChunkContentCoordinator(terrain);
        var installed = 0;
        coordinator.ContentInstalled += _ => installed++;

        Assert.False(coordinator.TryInstall(Snapshot(chunk.Coords, 2, 1, 10)));
        Assert.True(coordinator.TryInstall(Snapshot(
            chunk.Coords,
            chunk.AllocationGeneration,
            1,
            20)));
        Assert.False(coordinator.TryInstall(Snapshot(
            chunk.Coords,
            chunk.AllocationGeneration,
            1,
            30)));

        Assert.Equal(1, installed);
        Assert.True(chunk.IsLoaded);
        Assert.Equal(1, chunk.NetworkContentVersion);
        Assert.Equal(20, chunk.Cells[0]);
    }

    private static ClientChunkSnapshot Snapshot(
        Point2 coords,
        ulong generation,
        long version,
        int value)
    {
        var cells = new int[AuthorityChunkSnapshot.CellCount];
        cells[0] = value;
        return new ClientChunkSnapshot(
            new ChunkAllocationId(coords, generation),
            version,
            cells,
            new long[AuthorityChunkSnapshot.ShaftCount]);
    }
}
