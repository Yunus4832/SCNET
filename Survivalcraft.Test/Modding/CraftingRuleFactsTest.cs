using System.Xml.Linq;

using Game.Blocks;
using Game.Crafting;
using Game.Modding;
using Game.Modding.Blocks;
using Game.Subsystems;
using Game.Terrains;
using Game.World;

using Survivalcraft.Test.Fixtures;

namespace Survivalcraft.Test.Modding;

public sealed class CraftingRuleFactsTest
{
    [Theory]
    [InlineData(null, "2")]
    [InlineData(4f, "2")]
    [InlineData(5f, null)]
    public void MatchingUsesOnlyNullableLevelAndReturnsDeferredFeedback(float? level, string? messageKey)
    {
        using var project = new RuntimeTestProject(RuntimeSide.Authority);
        var info = new SubsystemGameInfo { WorldSettings = new WorldSettings() };
        project.AddSubsystem(info);
        var terrain = new SubsystemTerrain();
        project.InitializeSubsystem(terrain);
        var registry = new NamespacedRegistry<BlockRegistration>();
        registry.Register(
            new ModId("game"),
            ResourceId.Parse("game:air"),
            new BlockRegistration(static () => new AirBlock())
        );
        registry.Register(
            new ModId("test"),
            ResourceId.Parse("test:recipe"),
            new BlockRegistration(static () => new RecipeBlock())
        );
        using var catalog = new BlockContentCatalog(BlockRuntimeCatalog.Compile(registry.Entries));
        catalog.Initialize(new Game.Blocks.Collision.CollisionGeometryCatalog([]), null, new XElement("Recipes"));
        var recipe = catalog.Recipes.FindMatchingRecipe(
            terrain,
            ["recipe:0", "", "", "", "", "", "", "", ""],
            0f,
            level
        );
        Assert.NotNull(recipe);
        Assert.Equal(level, ((RecipeBlock)catalog.GetBlock(ResourceId.Parse("test:recipe"))).LastPlayerLevel);
        if (messageKey is null)
        {
            Assert.NotEqual(0, recipe.ResultValue);
        }
        else
        {
            Assert.Equal(0, recipe.ResultValue);
            Assert.Equal("CraftingRecipesManager", recipe.Message.Section);
            Assert.Equal(messageKey, recipe.Message.Key);
            Assert.Equal(5f, recipe.RequiredPlayerLevel);
        }
    }

    [Fact]
    public void AuthorityLoadsClothingRulesWithoutLoadingTextureAssets()
    {
        var registry = new NamespacedRegistry<BlockRegistration>();
        registry.Register(
            new ModId("game"),
            ResourceId.Parse("game:air"),
            new BlockRegistration(static () => new AirBlock())
        );
        registry.Register(
            new ModId("game"),
            ResourceId.Parse("game:clothing"),
            new BlockRegistration(static () => new ClothingBlock())
        );
        using var catalog = new BlockContentCatalog(BlockRuntimeCatalog.Compile(registry.Entries));
        catalog.Initialize(
            new Game.Blocks.Collision.CollisionGeometryCatalog([]),
            XElement.Parse(
                """
                <Clothes>
                  <ClothingData Index="0" DisplayName="Rule hat" Description="Rule description" Slot="Head"
                    ArmorProtection="0.1" Sturdiness="25" Insulation="4" MovementSpeedFactor="1"
                    SteedMovementSpeedFactor="1" DensityModifier="0" IsOuter="true" CanBeDyed="true" Layer="1"
                    PlayerLevelRequired="2" TextureName="missing/texture-that-must-not-load" ImpactSoundsFolder="Audio/Fabric" />
                </Clothes>
                """
            ),
            new XElement("Recipes")
        );
        var clothing = (ClothingBlock)catalog.GetBlock(ResourceId.Parse("game:clothing"));
        var rules = clothing.GetClothingData(0);
        Assert.Equal(0.1f, rules.ArmorProtection);
        Assert.Equal(2, rules.PlayerLevelRequired);
        Assert.Equal("missing/texture-that-must-not-load", rules.TextureName);
        Assert.Equal("Rule hat", rules.DisplayName);
    }

    private sealed class RecipeBlock : AirBlock
    {
        public float? LastPlayerLevel;

        public RecipeBlock()
        {
            CraftingId = "recipe";
        }

        public override CraftingRecipe? GetAdHocCraftingRecipe(
            SubsystemTerrain terrain,
            string[] ingredients,
            float heatLevel,
            float? playerLevel
        )
        {
            LastPlayerLevel = playerLevel;
            return new CraftingRecipe
            {
                Ingredients = ingredients,
                ResultValue = Terrain.MakeBlockValue(BlockIndex),
                ResultCount = 1,
                RequiredPlayerLevel = 5f
            };
        }
    }
}
