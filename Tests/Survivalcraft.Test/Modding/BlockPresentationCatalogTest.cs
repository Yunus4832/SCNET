using System.Xml.Linq;

using Game.Blocks;
using Game.Blocks.Presentation;
using Game.Modding;
using Game.Modding.Blocks;
using Game.Terrains;

namespace Survivalcraft.Test.Modding;

public sealed class BlockPresentationCatalogTest
{
    [Fact]
    public void IdentityBindingKeepsRulesAndVisualFieldsIndependent()
    {
        using var rules = CreateRules("Id;TextureSlot;DestructionDebrisScale\ngame:granite;3;0");
        using var presentations = new BlockPresentationCatalog(rules, Registrations());
        var granite = rules.GetBlock(ResourceId.Parse("game:granite"));
        var presentation = presentations.Get(granite.BlockIndex);
        Assert.Same(granite, presentation.Block);
        Assert.Same(presentation, presentations.Get(ResourceId.Parse("game:granite")));
        Assert.Equal(3, presentation.GetFaceTextureSlot(0, Terrain.MakeBlockValue(granite.BlockIndex)));
        var painted = Terrain.MakeBlockValue(granite.BlockIndex, PaintedCubeBlock.SetColor(0, 4));
        Assert.Equal(24, presentation.GetFaceTextureSlot(0, painted));
        Assert.Null(typeof(Block).GetMethod("DrawBlock"));
        Assert.Null(typeof(Block).GetField("TextureSlot"));
        Assert.Null(presentation.CreateDebrisParticleSystem(null!, default, painted, 1f));
    }

    [Fact]
    public void StairsCollisionDoesNotRequirePresentationInitialization()
    {
        using var rules = CreateRules();
        var stairs = rules.GetBlock(ResourceId.Parse("game:cobblestone_stairs"));
        for (var variant = 0; variant < 24; variant++)
        {
            Assert.NotEmpty(stairs.GetCustomCollisionBoxes(null!, Terrain.MakeBlockValue(stairs.BlockIndex, variant)));
        }
    }

    [Fact]
    public void MissingBindingsAndUnknownEmptyMetadataFailExplicitly()
    {
        using var rules = CreateRules();
        Assert.Throws<InvalidOperationException>(() => new BlockPresentationCatalog(rules, Registrations().Take(1)));
        Assert.Throws<InvalidDataException>(() => CreateRules("Id;UnknownColumn\ngame:granite;"));
    }

    [Fact]
    public void PreviewAndWorldOwnIndependentPresentationMetadata()
    {
        const string data = "Id;DisplayName\ngame:granite;Fixture Granite";
        using var previewRules = CreateRules(data);
        using var worldRules = CreateRules(data);
        using var preview = new BlockPresentationCatalog(previewRules, Registrations());
        using var world = new BlockPresentationCatalog(worldRules, Registrations());
        var id = ResourceId.Parse("game:granite");
        Assert.NotSame(preview.Get(id), world.Get(id));
        world.Get(id).DisplayName = "World Granite";
        Assert.Equal("Fixture Granite", preview.Get(id).DisplayName);
        Assert.Equal("World Granite", world.Get(id).DisplayName);
        world.Dispose();
        Assert.Equal("Fixture Granite", preview.Get(id).DisplayName);
    }

    [Fact]
    public void InvalidRuleDataReportsItsSourceWithoutLoadingClientLanguageResources()
    {
        using var rules = CreateRules();
        Assert.Throws<InvalidDataException>(() => CreateRules("\n\r\n"));
        var error = Assert.Throws<InvalidDataException>(() => CreateRules("Id;Durability\ngame:granite;1;2"));
        Assert.Contains("game:granite", error.Message);
        Assert.Contains("row 2", error.Message);
        Assert.Throws<KeyNotFoundException>(() => CreateRules("Id;Durability\nmissing:block;1"));
    }

    private static BlockContentCatalog CreateRules(string? data = null)
    {
        var registry = new NamespacedRegistry<BlockRegistration>();
        registry.Register(
            new ModId("game"),
            ResourceId.Parse("game:air"),
            new BlockRegistration(static () => new AirBlock())
        );
        registry.Register(
            new ModId("game"),
            ResourceId.Parse("game:granite"),
            new BlockRegistration(static () => new GraniteBlock())
        );
        registry.Register(
            new ModId("game"),
            ResourceId.Parse("game:cobblestone_stairs"),
            new BlockRegistration(static () => new CobblestoneStairsBlock())
        );
        var catalog = new BlockContentCatalog(
            BlockRuntimeCatalog.Compile(
                registry.Entries,
                data is null ? [] : [new BlockDataEntry(ResourceId.Parse("test:metadata"), data)]
            )
        );
        catalog.Initialize(new Game.Blocks.Collision.CollisionGeometryCatalog([]), null, new XElement("Recipes"));
        return catalog;
    }

    private static IEnumerable<BlockPresentationRegistration> Registrations() =>
    [
        new(
            ResourceId.Parse("game:air"),
            static (block, catalog) => new AirBlockPresentation((AirBlock)block, catalog)
        ),
        new(
            ResourceId.Parse("game:granite"),
            static (block, catalog) => new GraniteBlockPresentation((GraniteBlock)block, catalog)
        ),
        new(
            ResourceId.Parse("game:cobblestone_stairs"),
            static (block, catalog) => new CobblestoneStairsBlockPresentation((CobblestoneStairsBlock)block, catalog)
        )
    ];
}
