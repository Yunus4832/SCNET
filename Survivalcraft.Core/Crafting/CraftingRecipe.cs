using System.Globalization;

namespace Game.Crafting;

public class CraftingRecipe
{
    public bool HasMessage => IsDisabledResult || Message != LocalizedText.Empty;

    public LocalizedText CreateFeedbackText()
    {
        if (IsDisabledResult) { return new LocalizedText("ComponentMiner", "ItemDisabled", "This item is disabled"); }

        return RequiredPlayerLevel > 0f
            ? LocalizedText.Format(
                Message,
                LocalizedText.Literal(RequiredPlayerLevel.ToString(CultureInfo.InvariantCulture))
            )
            : Message;
    }

    public const int MaxSize = 3;

    public LocalizedText Description = LocalizedText.Empty;

    public LocalizedText DescriptionSuffix = LocalizedText.Empty;

    public int? DescriptionColor;

    public bool IsBlockDescription;

    public string[] Ingredients = new string[9];

    public bool IsAdHocCraftingRecipe;

    public LocalizedText Message = LocalizedText.Empty;

    public bool IsDisabledResult;

    public int RemainsCount;

    public int RemainsValue;

    public float RequiredHeatLevel;

    public float RequiredPlayerLevel;

    public int ResultCount;

    public int ResultValue;
}
