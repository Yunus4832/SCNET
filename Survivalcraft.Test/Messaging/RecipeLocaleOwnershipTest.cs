using System.Text.Json.Nodes;
using System.Xml.Linq;

using Game.Blocks;
using Game.Blocks.Collision;
using Game.Crafting;
using Game.Modding.Blocks;

using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Messaging;

[Collection(ConfigFileCollection.Name)]
public sealed class RecipeLocaleOwnershipTest
{
    [Theory]
    [InlineData("Recipe")]
    [InlineData("Description")]
    [InlineData("")]
    public void ResourceLookupPreservesPresentTranslationsEvenWhenTheyEqualLookupKeys(string translation)
    {
        var previous = Game.Localization.ClientLanguage.Catalog.KeyWords;
        try
        {
            Game.Localization.ClientLanguage.Catalog.KeyWords = new JsonObject
                { ["Recipe"] = new JsonObject { ["Description"] = translation } };
            Assert.Equal(
                translation,
                Game.Localization.ClientLanguage.Resolver.Resolve(
                    new LocalizedText("Recipe", "Description", "fallback")
                )
            );
            Assert.Equal(
                "fallback",
                Game.Localization.ClientLanguage.Resolver.Resolve(new LocalizedText("Recipe", "Missing", "fallback"))
            );
        }
        finally { Game.Localization.ClientLanguage.Catalog.KeyWords = previous; }
    }

    [Fact]
    public void ExistingRecipeFactsResolveNewLocaleWithoutRebuildingRulesOrMapping()
    {
        var registry = BlockRuntimeCatalog.Compile(
            [new(ResourceId.Parse("game:air"), new BlockRegistration(static () => new AirBlock()))]
        );
        using var catalog = new BlockContentCatalog(registry);
        catalog.Initialize(
            new CollisionGeometryCatalog([]),
            null,
            XElement.Parse(
                """
                <Recipes><Recipe Result="game:air" ResultCount="1" Description="fixture"
                    RequiredHeatLevel="0" a="AirBlock">"a"</Recipe></Recipes>
                """
            )
        );
        var recipe = Assert.Single(catalog.Recipes.ReadonlyRecipes);
        recipe.Description = new LocalizedText("Recipe", "Description", "fallback");
        recipe.Message = new LocalizedText("Recipe", "Message", "requires {0}");
        recipe.RequiredPlayerLevel = 5;
        var feedback = recipe.CreateFeedbackText();
        var mapping = catalog.Registry.Mapping;
        var previous = Game.Localization.ClientLanguage.Catalog.KeyWords;
        try
        {
            Game.Localization.ClientLanguage.Catalog.KeyWords = new JsonObject
                { ["Recipe"] = new JsonObject { ["Description"] = "Recipe", ["Message"] = "requires {0}" } };
            Assert.Equal("Recipe", CraftingRecipeText.ResolveDescription(recipe));
            Assert.Equal("requires 5", Game.Localization.ClientLanguage.Resolver.Resolve(feedback));
            Game.Localization.ClientLanguage.Catalog.KeyWords = new JsonObject
                { ["Recipe"] = new JsonObject { ["Description"] = "配方", ["Message"] = "需要等级 {0}" } };
            Assert.Equal("配方", CraftingRecipeText.ResolveDescription(recipe));
            Assert.Equal("需要等级 5", Game.Localization.ClientLanguage.Resolver.Resolve(feedback));
            Assert.Same(recipe, Assert.Single(catalog.Recipes.ReadonlyRecipes));
            Assert.Same(mapping, catalog.Registry.Mapping);
            Assert.Equal(LocalizedTextKind.Format, feedback.Kind);
            Assert.Same(recipe.Message, feedback.Template);
        }
        finally { Game.Localization.ClientLanguage.Catalog.KeyWords = previous; }
    }
}
