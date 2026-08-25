using Game;
using Game.Terrains;
using Game.Terrains.Distribution;

namespace Survivalcraft.Test.Terrains.Distribution;

public sealed class ClientDerivedTerrainPolicyTest
{
    [Fact]
    public void ClientDoesNotRecursivelyAdvanceMissingNetworkNeighbor()
    {
        var neighbor = new TerrainChunk(null!, 1, 2)
        {
            WorkerState = TerrainChunkState.NotLoaded
        };

        Assert.False(ClientDerivedTerrainPolicy.CanAdvanceLightingDependency(
            TerrainContentRole.Replica,
            neighbor));
        Assert.True(ClientDerivedTerrainPolicy.CanAdvanceLightingDependency(
            TerrainContentRole.Authority,
            neighbor));

        neighbor.IsLoaded = true;
        Assert.True(ClientDerivedTerrainPolicy.CanAdvanceLightingDependency(
            TerrainContentRole.Replica,
            neighbor));
    }

    [Fact]
    public void SchedulerSelectsLoadedDependencyWithoutRecursing()
    {
        var terrain = new Terrain();
        var target = terrain.AllocateChunk(0, 0);
        var dependency = terrain.AllocateChunk(1, 0);
        target.IsLoaded = true;
        target.WorkerState = TerrainChunkState.InvalidPropagatedLight;
        dependency.IsLoaded = true;
        dependency.WorkerState = TerrainChunkState.InvalidLight;

        Assert.Same(
            dependency,
            ClientDerivedTerrainPolicy.FindPendingLightingDependency(
                terrain,
                TerrainContentRole.Replica,
                target));

        dependency.IsLoaded = false;
        Assert.Null(ClientDerivedTerrainPolicy.FindPendingLightingDependency(
            terrain,
            TerrainContentRole.Replica,
            target));
        Assert.Same(
            dependency,
            ClientDerivedTerrainPolicy.FindPendingLightingDependency(
                terrain,
                TerrainContentRole.Authority,
                target));
    }
}
