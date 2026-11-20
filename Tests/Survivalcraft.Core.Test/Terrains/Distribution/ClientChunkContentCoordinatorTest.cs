using Engine.Core.Mathematics;

using Game.Protocol.Terrain;
using Game.Terrains;
using Game.Terrains.Distribution;

namespace Survivalcraft.Core.Test.Terrains.Distribution;

public sealed class ClientChunkContentCoordinatorTest
{
    [Fact]
    public void NewerBaselineReplacesInstalledContentWithoutARecoveryRequest()
    {
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(1, 2);
        var coordinator = new ClientChunkContentCoordinator(terrain);

        Assert.True(coordinator.TryInstall(Snapshot(chunk.Coords, chunk.AllocationGeneration, 100, 10)));
        Assert.False(chunk.NeedsContentSnapshot);
        Assert.True(coordinator.TryInstall(Snapshot(chunk.Coords, chunk.AllocationGeneration, 200, 20)));
        Assert.False(coordinator.TryInstall(Snapshot(chunk.Coords, chunk.AllocationGeneration, 199, 30)));
        Assert.False(coordinator.TryInstall(Snapshot(chunk.Coords, chunk.AllocationGeneration, 200, 40)));
        Assert.Equal(200, chunk.NetworkContentVersion);
        Assert.Equal(20, chunk.Cells[0]);

        chunk.NeedsContentValidation = true;
        Assert.True(coordinator.TryInstall(Snapshot(chunk.Coords, chunk.AllocationGeneration, 200, 50)));
        Assert.False(chunk.NeedsContentValidation);
        Assert.Equal(50, chunk.Cells[0]);
    }

    private static ClientChunkSnapshot Snapshot(Point2 coords, ulong generation, long version, int value)
    {
        var cells = new int[AuthorityChunkSnapshot.CellCount];
        cells[0] = value;
        return new ClientChunkSnapshot(
            new ChunkAllocationId(coords, generation),
            version,
            cells,
            new long[AuthorityChunkSnapshot.ShaftCount]
        );
    }

    [Fact]
    public void ReallocatedTerrainRejectsThePreviousAllocationsSnapshot()
    {
        using var terrain = new Terrain();
        var first = terrain.AllocateChunk(1, 2);
        var pending = Snapshot(first.Coords, first.AllocationGeneration, 1, 10);
        terrain.FreeChunk(first);
        var replacement = terrain.AllocateChunk(1, 2);
        var coordinator = new ClientChunkContentCoordinator(terrain);

        Assert.NotEqual(first.AllocationGeneration, replacement.AllocationGeneration);
        Assert.False(coordinator.TryInstall(pending));
        Assert.True(coordinator.TryInstall(Snapshot(replacement.Coords, replacement.AllocationGeneration, 1, 20)));
        Assert.Equal(20, replacement.Cells[0]);
    }

    [Fact]
    public void InstallsOnlyCurrentAllocationAndNewerContentVersion()
    {
        var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(1, 2);
        var coordinator = new ClientChunkContentCoordinator(terrain);
        var installed = 0;
        coordinator.ContentInstalled += _ => installed++;

        Assert.False(coordinator.TryInstall(Snapshot(chunk.Coords, 2, 1, 10)));
        Assert.True(
            coordinator.TryInstall(
                Snapshot(
                    chunk.Coords,
                    chunk.AllocationGeneration,
                    1,
                    20
                )
            )
        );
        Assert.False(
            coordinator.TryInstall(
                Snapshot(
                    chunk.Coords,
                    chunk.AllocationGeneration,
                    1,
                    30
                )
            )
        );

        Assert.Equal(1, installed);
        Assert.True(chunk.HasContents);
        Assert.Equal(1, chunk.NetworkContentVersion);
        Assert.Equal(20, chunk.Cells[0]);
    }
}
