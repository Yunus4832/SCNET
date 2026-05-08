namespace Game;

public class CraftingRecipe
{
    public const int MaxSize = 3;

    public string Description = string.Empty;

    public string[] Ingredients = new string[9];

    public bool IsAdHocCraftingRecipe;

    public string Message = string.Empty;

    public int RemainsCount;

    public int RemainsValue;

    public float RequiredHeatLevel;

    public float RequiredPlayerLevel;

    public int ResultCount;

    public int ResultValue;
}
