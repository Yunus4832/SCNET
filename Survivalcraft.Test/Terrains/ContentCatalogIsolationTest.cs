using Game.Blocks;
using Game.Furniture;
using Game.Modding;
using Game.Modding.Blocks;
using Game.Subsystems;
using Game.Terrains;
using Game.World;

using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Terrains;

[Collection(ConfigFileCollection.Name)]
public sealed class ContentCatalogIsolationTest
{
    private static readonly ResourceId _paintId = ResourceId.Parse("test:paint");

    [Fact]
    public void FurnitureAndItsCloneKeepTheirOwnPaintDefinition()
    {
        using var first = new WorldCatalogScope(Descriptor(11));
        using var second = new WorldCatalogScope(RuntimeSide.Client, Descriptor(22));
        var firstDesign = new FurnitureDesign(first.Catalog, null);
        var secondDesign = new FurnitureDesign(second.Catalog, null);
        firstDesign.SetValues(2, Enumerable.Repeat(first.Catalog.GetBlock(_paintId).BlockIndex, 8).ToArray());
        secondDesign.SetValues(2, Enumerable.Repeat(second.Catalog.GetBlock(_paintId).BlockIndex, 8).ToArray());
        var clone = firstDesign.Clone();

        firstDesign.Paint(1);
        secondDesign.Paint(1);
        clone.Paint(1);

        Assert.Equal(11, Terrain.ExtractData(firstDesign.GetValue(0)));
        Assert.Equal(22, Terrain.ExtractData(secondDesign.GetValue(0)));
        Assert.Equal(11, Terrain.ExtractData(clone.GetValue(0)));
    }

    [Fact]
    public void FurnitureRejectsAnotherWorldsCatalog()
    {
        using var first = new WorldCatalogScope();
        using var second = new WorldCatalogScope(RuntimeSide.Client);
        var terrain = new SubsystemTerrain();
        first.Project.InitializeSubsystem(terrain);

        Assert.Throws<ArgumentException>(() => new FurnitureDesign(second.Catalog, terrain));
    }

    [Fact]
    public void DataOnlyFurnitureCanRoundTripButCannotResolveWorldContent()
    {
        var design = new FurnitureDesign(null, null);
        design.SetValues(2, Enumerable.Repeat(3, 8).ToArray());
        var restored = new FurnitureDesign(7, null, null, design.Save());

        Assert.Equal(3, restored.GetValue(0));
        Assert.Throws<InvalidOperationException>(() => restored.Paint(1));
    }

    [Fact]
    public void DisabledBlocksResolveAgainstTheSuppliedWorld()
    {
        using var first = new WorldCatalogScope();
        using var second = new WorldCatalogScope(RuntimeSide.Client);
        first.Catalog.Blocks[3].CraftingId = "scoped";
        second.Catalog.Blocks[4].CraftingId = "scoped";
        var settings = new WorldSettings { DisableBlocks = "scoped:0" };

        Assert.True(BlockAvailability.IsBlockDisabled(first.Catalog, settings.DisableBlocks, 3));
        Assert.False(BlockAvailability.IsBlockDisabled(second.Catalog, settings.DisableBlocks, 3));
        Assert.True(BlockAvailability.IsBlockDisabled(second.Catalog, settings.DisableBlocks, 4));
    }

    private static ModDescriptor Descriptor(int marker)
    {
        return new ModDescriptor(new ModManifest("test", "Paint", "1.0.0"), () => new PaintMod(marker));
    }

    private sealed class PaintMod(int marker) : IMod
    {
        public void Configure(IModContext context)
        {
            context.Extensions.Register(
                BlockExtensions.RegistryName,
                _paintId,
                new BlockRegistration(() => new PaintBlock(marker))
            );
        }

        public void Start(IModContext context)
        {
        }

        public void Stop()
        {
        }
    }

    private sealed class PaintBlock(int marker) : CubeBlock, IPaintableBlock
    {
        public int? GetPaintColor(int value) => Terrain.ExtractData(value);

        public int Paint(SubsystemTerrain? subsystemTerrain, int value, int? color)
        {
            return Terrain.ReplaceData(value, marker);
        }
    }
}
