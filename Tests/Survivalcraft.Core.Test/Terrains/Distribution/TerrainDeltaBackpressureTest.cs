using Engine.Core.Mathematics;

using Game.Protocol.Terrain;
using Game.Terrains;
using Game.Terrains.Distribution;

namespace Survivalcraft.Core.Test.Terrains.Distribution;

public sealed class TerrainDeltaBackpressureTest
{
    [Fact]
    public void RecoveryCannotInvalidateAReallocatedOrAlreadyNewerChunk()
    {
        using var terrain = new Terrain();
        var old = terrain.AllocateChunk(0, 0);
        var allocation = new ChunkAllocationId(old.Coords, old.AllocationGeneration);
        terrain.FreeChunk(old);
        var current = terrain.AllocateChunk(0, 0);
        current.HasContents = true;
        current.NetworkContentVersion = 100;
        var coordinator = new ClientTerrainDeltaCoordinator(terrain, _ => { });
        Assert.Equal(
            TerrainDeltaApplyResult.Ignored,
            coordinator.RequireSnapshot(new ChunkContentResync(allocation, 1000))
        );
        var replacement = new ChunkAllocationId(current.Coords, current.AllocationGeneration);
        Assert.Equal(
            TerrainDeltaApplyResult.Ignored,
            coordinator.RequireSnapshot(new ChunkContentResync(replacement, 99))
        );
        Assert.True(current.HasContents);
    }

    [Fact]
    public void CoordinatorBoundsPendingDeltasAndReleasesCapacityWithoutDuplicatesSpendingIt()
    {
        using var terrain = new Terrain();
        var coordinator = new ClientTerrainDeltaCoordinator(terrain, _ => { });
        var chunks = Enumerable.Range(0, 33).Select(x => terrain.AllocateChunk(x, 0)).ToArray();
        foreach (var chunk in chunks.Take(32))
        {
            for (var version = 1; version <= 256; version++)
            {
                var delta = new TerrainCellDelta(new Point3(chunk.Origin.X, 64, 0), 7, version, version + 1);
                coordinator.Receive(delta);
                coordinator.Receive(delta);
            }
        }

        Assert.Equal(ClientTerrainDeltaCoordinator.MaximumPendingDeltas, coordinator.PendingDeltaCount);
        Assert.Equal(
            TerrainDeltaApplyResult.ResyncRequired,
            coordinator.Receive(new TerrainCellDelta(new Point3(chunks[^1].Origin.X, 64, 0), 7, 1, 2))
        );
        Assert.Equal(ClientTerrainDeltaCoordinator.MaximumPendingDeltas, coordinator.PendingDeltaCount);
        coordinator.Discard(chunks[0].Coords);
        Assert.Equal(ClientTerrainDeltaCoordinator.MaximumPendingDeltas - 256, coordinator.PendingDeltaCount);
    }
}
