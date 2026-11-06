using Engine.Core.Mathematics;

using Game.Blocks;
using Game.Modding;
using Game.Modding.Blocks;
using Game.Subsystems;
using Game.Subsystems.Behavior;
using Game.Terrains;
using Game.World;

using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Terrains;

[Collection(ConfigFileCollection.Name)]
public sealed class TerrainCatalogIsolationTest
{
    private static readonly ResourceId _blockId = ResourceId.Parse("test:collision");

    [Fact]
    public void RaycastsAndSunlightUseTheirOwnWorldWhileAnotherWorldIsCurrent()
    {
        using var solidWorld = new WorldCatalogScope(Descriptor(true));
        using var transparentWorld = new WorldCatalogScope(RuntimeSide.Client, Descriptor(false));
        using var solidTerrain = new Terrain();
        using var transparentTerrain = new Terrain();
        var solid = new SubsystemTerrain { Terrain = solidTerrain };
        var transparent = new SubsystemTerrain { Terrain = transparentTerrain };
        solidWorld.Project.InitializeSubsystem(solid);
        transparentWorld.Project.InitializeSubsystem(transparent);
        var solidChunk = solidTerrain.AllocateChunk(0, 0);
        var transparentChunk = transparentTerrain.AllocateChunk(0, 0);
        solidWorld.Catalog.Blocks[0].Transparent = true;
        transparentWorld.Catalog.Blocks[0].Transparent = true;
        solidChunk.SetCellValueFast(1, 64, 1, solid.Catalog.GetBlock(_blockId).BlockIndex);
        transparentChunk.SetCellValueFast(1, 64, 1, transparent.Catalog.GetBlock(_blockId).BlockIndex);
        var start = new Vector3(1.5f, 66, 1.5f);
        var end = new Vector3(1.5f, 63, 1.5f);

        Assert.NotNull(solid.Raycast(start, end, false, true, null));
        Assert.Null(transparent.Raycast(start, end, false, true, null));
        var solidValue = solid.Catalog.GetBlock(_blockId).BlockIndex;
        var transparentValue = transparent.Catalog.GetBlock(_blockId).BlockIndex;
        Assert.True(SubsystemSnowBlockBehavior.CanSupportSnow(solid.Catalog, solidValue));
        Assert.False(SubsystemSnowBlockBehavior.CanSupportSnow(transparent.Catalog, transparentValue));
        Assert.True(SubsystemFallenLeavesBlockBehavior.CanSupportFallenLeaves(solid.Catalog, solidValue));
        Assert.False(SubsystemFallenLeavesBlockBehavior.CanSupportFallenLeaves(transparent.Catalog, transparentValue));
        TerrainUpdater.GenerateChunkSunLightAndHeight(solidChunk, 15, solid.Catalog);
        TerrainUpdater.GenerateChunkSunLightAndHeight(transparentChunk, 15, transparent.Catalog);
        Assert.Equal(0, solidChunk.GetCellLightFast(1, 64, 1));
        Assert.Equal(15, transparentChunk.GetCellLightFast(1, 64, 1));
        Assert.NotNull(solid.Raycast(start, end, false, true, null));
    }

    private static ModDescriptor Descriptor(bool solid)
    {
        return new ModDescriptor(new ModManifest("test", "Collision", "1.0.0"), () => new CollisionMod(solid));
    }

    private sealed class CollisionMod(bool solid) : IMod
    {
        public void Configure(IModContext context)
        {
            context.Extensions.Register(
                BlockExtensions.RegistryName,
                _blockId,
                new BlockRegistration(() => new CollisionBlock(solid))
            );
        }

        public void Start(IModContext context)
        {
        }

        public void Stop()
        {
        }
    }

    private sealed class CollisionBlock : CubeBlock
    {
        private readonly bool _solid;

        public CollisionBlock(bool solid)
        {
            _solid = solid;
            Transparent = !solid;
        }

        public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
        {
            return _solid ? DefaultCollisionBoxes : [];
        }

        public override bool IsFaceTransparent(SubsystemTerrain subsystemTerrain, int face, int value) => !_solid;
    }
}
