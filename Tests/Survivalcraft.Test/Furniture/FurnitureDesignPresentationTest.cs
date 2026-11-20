using Game.Blocks;
using Game.Blocks.Presentation;
using Game.Furniture;
using Game.Modding;
using Game.Modding.Blocks;

namespace Survivalcraft.Test.Furniture;

public sealed class FurnitureDesignPresentationTest
{
    [Fact]
    public void RevisionInvalidatesOnlyTheClientGeometryCache()
    {
        using var rules = CreateRules();
        using var catalog = CreatePresentations(rules);
        var design = new FurnitureDesign(rules, null);
        design.SetValues(2, Enumerable.Repeat(3, 8).ToArray());
        var revision = design.Revision;
        var geometry = catalog.Furniture.GetGeometry(design, null);
        Assert.True(geometry.SubsetOpaqueByFace.Sum(mesh => mesh?.Indices.Count ?? 0) > 0);
        Assert.Same(geometry, catalog.Furniture.GetGeometry(design, null));
        design.SetValues(2, new int[8]);
        Assert.Equal(revision + 1, design.Revision);
        var empty = catalog.Furniture.GetGeometry(design, null);
        Assert.NotSame(geometry, empty);
        Assert.Equal(0, empty.SubsetOpaqueByFace.Sum(mesh => mesh?.Indices.Count ?? 0));
        Assert.Null(typeof(FurnitureDesign).GetProperty("Geometry"));
        Assert.Null(typeof(FurnitureDesign).GetMethod("GetDefaultName"));
    }

    [Fact]
    public void DifferentRuleCatalogsCannotShareFurnitureGeometry()
    {
        using var firstRules = CreateRules();
        using var secondRules = CreateRules();
        using var first = CreatePresentations(firstRules);
        using var second = CreatePresentations(secondRules);
        var firstDesign = new FurnitureDesign(firstRules, null);
        var secondDesign = new FurnitureDesign(secondRules, null);
        firstDesign.SetValues(2, Enumerable.Repeat(3, 8).ToArray());
        secondDesign.SetValues(2, Enumerable.Repeat(3, 8).ToArray());
        Assert.Throws<ArgumentException>(() => first.Furniture.GetGeometry(secondDesign, null));
        Assert.NotSame(
            first.Furniture.GetGeometry(firstDesign, null),
            second.Furniture.GetGeometry(secondDesign, null)
        );
    }

    private static BlockContentCatalog CreateRules()
    {
        var registrations = new NamespacedRegistry<BlockRegistration>();
        registrations.Register(
            new ModId("game"),
            ResourceId.Parse("game:air"),
            new BlockRegistration(static () => new AirBlock())
        );
        registrations.Register(
            new ModId("game"),
            ResourceId.Parse("game:granite"),
            new BlockRegistration(static () => new GraniteBlock())
        );
        return new BlockContentCatalog(BlockRuntimeCatalog.Compile(registrations.Entries));
    }

    private static BlockPresentationCatalog CreatePresentations(BlockContentCatalog rules) => new(
        rules,
        [
            new(
                ResourceId.Parse("game:air"),
                static (block, catalog) => new AirBlockPresentation((AirBlock)block, catalog)
            ),
            new(
                ResourceId.Parse("game:granite"),
                static (block, catalog) => new GraniteBlockPresentation((GraniteBlock)block, catalog)
            )
        ]
    );
}
