using Game.World.Presentation;

namespace Game.Crafting;

public static class CraftingRecipeText
{
    public static string ResolveDescription(CraftingRecipe recipe)
    {
        var text = recipe.IsBlockDescription
            ? Localization.ClientLanguage.Catalog.GetBlock(recipe.Description.Section, recipe.Description.Key)
            : Localization.ClientLanguage.Resolver.Resolve(recipe.Description);
        if (recipe.DescriptionColor is { } color)
        {
            text += " " + PalettePresentation.GetName(color, string.Empty);
        }

        if (recipe.DescriptionSuffix != LocalizedText.Empty)
        {
            text += " " + Localization.ClientLanguage.Resolver.Resolve(recipe.DescriptionSuffix);
        }

        return text;
    }
}
