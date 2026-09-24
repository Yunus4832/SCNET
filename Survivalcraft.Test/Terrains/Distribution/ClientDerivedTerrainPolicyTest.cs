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

    [Fact]
    public void GeometryWaitsForAdjacentLoadedChunkToFinishLighting()
    {
        var terrain = new Terrain();
        var target = terrain.AllocateChunk(0, 0);
        var dependency = terrain.AllocateChunk(1, 0);
        target.IsLoaded = true;
        target.WorkerState = TerrainChunkState.InvalidVertices1;
        dependency.IsLoaded = true;
        dependency.WorkerState = TerrainChunkState.InvalidPropagatedLight;

        Assert.Same(
            dependency,
            ClientDerivedTerrainPolicy.FindPendingGeometryDependency(
                terrain,
                TerrainContentRole.Replica,
                target));

        dependency.WorkerState = TerrainChunkState.InvalidVertices1;
        Assert.Null(ClientDerivedTerrainPolicy.FindPendingGeometryDependency(
            terrain,
            TerrainContentRole.Replica,
            target));
    }

    [Fact]
    public void GeometryDependencyStillHonorsItsOwnLightingDependency()
    {
        var terrain = new Terrain();
        var target = terrain.AllocateChunk(0, 0);
        var geometryDependency = terrain.AllocateChunk(1, 0);
        var lightingDependency = terrain.AllocateChunk(2, 0);
        target.IsLoaded = true;
        target.WorkerState = TerrainChunkState.InvalidVertices1;
        geometryDependency.IsLoaded = true;
        geometryDependency.WorkerState = TerrainChunkState.InvalidPropagatedLight;
        lightingDependency.IsLoaded = true;
        lightingDependency.WorkerState = TerrainChunkState.InvalidLight;

        var pendingGeometry = ClientDerivedTerrainPolicy.FindPendingGeometryDependency(
            terrain,
            TerrainContentRole.Replica,
            target);

        Assert.Same(geometryDependency, pendingGeometry);
        Assert.Same(
            lightingDependency,
            ClientDerivedTerrainPolicy.FindPendingLightingDependency(
                terrain,
                TerrainContentRole.Replica,
                pendingGeometry!));
    }
}
