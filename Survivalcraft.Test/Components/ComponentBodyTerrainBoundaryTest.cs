using System.Reflection;

using Engine.Core.Collections;
using Engine.Core.Mathematics;

using Game.Blocks;
using Game.Components;
using Game.Subsystems;
using Game.Terrains;
using Game.Terrains.Distribution;
using Game.World;

using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Components;

[Collection(ConfigFileCollection.Name)]
public sealed class ComponentBodyTerrainBoundaryTest
{
    [Theory]
    [InlineData(RuntimeSide.Authority)]
    [InlineData(RuntimeSide.Client)]
    public void TheRealBodySolverBlocksOnlyTheUnknownDirectionAndAllowsRetreat(RuntimeSide side)
    {
        using var scope = new WorldCatalogScope(side);
        using var terrain = new Terrain();
        terrain.AllocateChunk(0, 0).SetMainThreadState(TerrainChunkState.InvalidLight);
        var body = CreateBody(scope, terrain);
        body.Velocity = new Vector3(4, -1, 2);

        body.MoveWithCollision(0.1f, new Vector3(2, 0, 0.5f));

        Assert.Equal(15.7f, body.Position.X, 4);
        Assert.Equal(8.5f, body.Position.Z, 4);
        Assert.True(body.IsTerrainReady);
        Assert.Equal(new Vector3(0, -1, 2), body.Velocity);
        body.MoveWithCollision(0.1f, new Vector3(-1, 0, 1));
        Assert.Equal(14.7f, body.Position.X, 4);
        Assert.Equal(9.5f, body.Position.Z, 4);

        terrain.AllocateChunk(1, 0).SetMainThreadState(TerrainChunkState.InvalidLight);
        body.MoveWithCollision(0.1f, new Vector3(2, 0, 0));
        Assert.Equal(16.7f, body.Position.X, 4);
        Assert.True(body.IsTerrainReady);
    }

    [Fact]
    public void IncompleteAllocatedContentsAreNotReadAsPhysicalBlocks()
    {
        using var scope = new WorldCatalogScope();
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(1, 0);
        chunk.SetMainThreadState(TerrainChunkState.InvalidContents4);
        chunk.SetCellValueFast(0, 70, 8, Terrain.MakeBlockValue(1));
        var body = CreateBody(scope, terrain);
        var collisions = new DynamicArray<ComponentBody.CollisionBox>();

        body.FindTerrainCollisionBoxes(new BoundingBox(new Vector3(16, 70, 8), new Vector3(17, 71, 9)), collisions);

        Assert.Empty(collisions);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DepenetrationCannotPushABodyIntoAnUnknownChunk(bool unrestricted)
    {
        using var scope = new WorldCatalogScope();
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        chunk.SetMainThreadState(TerrainChunkState.InvalidLight);
        scope.Catalog.Blocks[1] = new GraniteBlock { Collidable = true };
        for (var y = 0; y < 256; y++)
        {
            for (var z = 0; z < 16; z++) { chunk.SetCellValueFast(15, y, z, Terrain.MakeBlockValue(1)); }
        }

        var body = CreateBody(scope, terrain);
        body.Position = new Vector3(15.9f, 70, 8);
        if (unrestricted) { body.MoveToFreeSpace(float.PositiveInfinity); }
        else { body.MoveToFreeSpace(); }

        Assert.True(body.IsTerrainReady);
        Assert.True(body.Position.X < 16f);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SnapshotRecoveryKeepsTheRealBodyMobileAndPreservesExistingWallCollisions(bool overflow)
    {
        using var scope = new WorldCatalogScope(RuntimeSide.Client);
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        chunk.IsLoaded = true;
        chunk.SetMainThreadState(TerrainChunkState.Ready);
        chunk.SetWorkerState(TerrainChunkState.Ready);
        chunk.NetworkContentVersion = 10;
        scope.Catalog.Blocks[1] = new GraniteBlock { Collidable = true };
        for (var y = 0; y < 256; y++)
        {
            for (var x = 0; x < 16; x++) { chunk.SetCellValueFast(x, y, 10, Terrain.MakeBlockValue(1)); }
        }

        var coordinator = new ClientTerrainDeltaCoordinator(
            terrain,
            _ => throw new InvalidOperationException("Unexpected delta.")
        );
        var result = overflow
            ? coordinator.RequireSnapshot(
                new ChunkContentResync(new ChunkAllocationId(chunk.Coords, chunk.AllocationGeneration), 12)
            )
            : coordinator.Receive(new TerrainCellDelta(new Point3(1, 70, 1), 0, 11, 12));
        Assert.Equal(TerrainDeltaApplyResult.ResyncRequired, result);
        var body = CreateBody(scope, terrain);
        Assert.True(body.IsTerrainReady);
        body.MoveWithCollision(0.1f, new Vector3(-1, 0, 0));
        Assert.Equal(14.2f, body.Position.X, 4);
        for (var step = 0; step < 40; step++) { body.MoveWithCollision(0.1f, new Vector3(0, 0, 0.1f)); }

        Assert.True(body.Position.Z < 10f);
        Assert.True(body.IsTerrainReady);
        var collisions = new DynamicArray<ComponentBody.CollisionBox>();
        body.FindTerrainCollisionBoxes(new BoundingBox(new Vector3(14, 70, 10), new Vector3(15, 71, 11)), collisions);
        Assert.NotEmpty(collisions);
    }

    private static ComponentBody CreateBody(WorldCatalogScope scope, Terrain terrain)
    {
        var body = new ComponentBody
        {
            Position = new Vector3(15.2f, 70, 8),
            Rotation = Quaternion.Identity,
            BoxSize = new Vector3(0.6f, 1.8f, 0.6f)
        };
        scope.Project.InitializeComponent(body);
        typeof(ComponentBody).GetField("_subsystemTerrain", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(body, new SubsystemTerrain { Terrain = terrain });
        return body;
    }
}
