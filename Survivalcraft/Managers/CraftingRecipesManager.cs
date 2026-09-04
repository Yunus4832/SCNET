using System.Globalization;
using System.Xml.Linq;

using EntitySystem.XmlUtilities;

namespace Game.Managers;

public static class CraftingRecipesManager
{
    private static readonly List<CraftingRecipe> _recipes = [];

    private static Action _initialize1 = Actions.Empty;

    private static Action? _initialized = Actions.Empty;

    private const string _typeName = nameof(CraftingRecipesManager);

    public static ReadOnlyList<CraftingRecipe> ReadonlyRecipes => new(_recipes);

    public static void Initialize()
    {
        var runtime = CurrentModRuntime.Value
                      ?? throw new InvalidOperationException("No active game mod runtime.");
        runtime.InitializeCraftingRecipes();
    }

    public static void Initialize(XElement source)
    {
        Initialize(source, false);
    }

    private static void Initialize(XElement source, bool useLegacyHooks)
    {
        _recipes.Clear();
        var source2 = source.Descendants("Recipe");
        foreach (var item in source2)
        {
            var craftingRecipe = new CraftingRecipe();
            var attributeValue = XmlUtils.GetAttributeValue<string>(item, "Result");
            var desc = XmlUtils.GetAttributeValue<string>(item, "Description");
            if (desc.StartsWith('[') && desc.EndsWith(']'))
            {
                desc = LanguageManager.GetBlock(attributeValue,
                    string.Concat("CRDescription:", desc.AsSpan(1, desc.Length - 2)));
            }

            craftingRecipe.ResultValue = DecodeResult(attributeValue, useLegacyHooks);
            craftingRecipe.ResultCount = XmlUtils.GetAttributeValue<int>(item, "ResultCount");
            var attributeValue2 = XmlUtils.GetAttributeValue(item, "Remains", string.Empty);
            if (!string.IsNullOrEmpty(attributeValue2))
            {
                craftingRecipe.RemainsValue = DecodeResult(attributeValue2, useLegacyHooks);
                craftingRecipe.RemainsCount = XmlUtils.GetAttributeValue<int>(item, "RemainsCount");
            }

            craftingRecipe.RequiredHeatLevel = XmlUtils.GetAttributeValue<float>(item, "RequiredHeatLevel");
            craftingRecipe.RequiredPlayerLevel = XmlUtils.GetAttributeValue(item, "RequiredPlayerLevel", 1f);
            craftingRecipe.Description = desc;
            craftingRecipe.Message = XmlUtils.GetAttributeValue(item, "Message", string.Empty);
            if (craftingRecipe.ResultCount >
                BlocksManager.Blocks[Terrain.ExtractContents(craftingRecipe.ResultValue)].MaxStacking)
            {
                throw new InvalidOperationException(
                    $"In recipe for \"{attributeValue}\" ResultCount is larger than max stacking of result block.");
            }

            if (craftingRecipe.RemainsValue != 0 && craftingRecipe.RemainsCount >
                BlocksManager.Blocks[Terrain.ExtractContents(craftingRecipe.RemainsValue)].MaxStacking)
            {
                throw new InvalidOperationException(
                    $"In Recipe for \"{attributeValue2}\" RemainsCount is larger than max stacking of remains block.");
            }

            var dictionary = new Dictionary<char, string>();
            foreach (var item2 in from a in item.Attributes()
                                  where a.Name.LocalName.Length == 1 && char.IsLower(a.Name.LocalName[0])
                                  select a)
            {
                DecodeIngredient(item2.Value, useLegacyHooks, out var craftingId, out var data);
                if (BlocksManager.FindBlocksByCraftingId(craftingId).Length == 0)
                {
                    throw new InvalidOperationException($"Block with craftingId \"{item2.Value}\" not found.");
                }

                if (data is < 0 or > 262143)
                {
                    throw new InvalidOperationException(
                        $"Data in recipe ingredient \"{item2.Value}\" must be between 0 and 0x3FFFF.");
                }

                dictionary.Add(item2.Name.LocalName[0], item2.Value);
            }

            var array = item.Value.Trim().Split(["\n"], StringSplitOptions.None);
            for (var i = 0; i < array.Length; i++)
            {
                var num = array[i].IndexOf('"');
                var num2 = array[i].LastIndexOf('"');
                if (num < 0 || num2 < 0 || num2 <= num)
                {
                    throw new InvalidOperationException("Invalid recipe line.");
                }

                var text = array[i].Substring(num + 1, num2 - num - 1);
                for (var j = 0; j < text.Length; j++)
                {
                    var c = text[j];
                    if (!char.IsLower(c))
                    {
                        continue;
                    }

                    var text2 = dictionary[c];
                    craftingRecipe.Ingredients[j + i * 3] = text2;
                }
            }

            _recipes.Add(craftingRecipe);
        }

        var blocks = BlocksManager.Blocks;
        foreach (var block in blocks)
        {
            _recipes.AddRange(block.GetProceduralCraftingRecipes());
        }

        _initialized?.Invoke();
        _recipes.Sort(delegate (CraftingRecipe r1, CraftingRecipe r2)
        {
            var y = r1.Ingredients.Count(s => !string.IsNullOrEmpty(s));
            var x = r2.Ingredients.Count(s => !string.IsNullOrEmpty(s));
            return Comparer<int>.Default.Compare(x, y);
        });
    }

    public static CraftingRecipe? FindMatchingRecipe(
        SubsystemTerrain terrain,
        string[] ingredients,
        float heatLevel,
        ComponentPlayer? componentPlayer
    )
    {
        var playerLevel = componentPlayer?.PlayerData.Level ?? 1f;
        CraftingRecipe? craftingRecipe = null;
        var blocks = BlocksManager.Blocks;
        foreach (var block in blocks)
        {
            var adHocCraftingRecipe = block.GetAdHocCraftingRecipe(
                terrain,
                ingredients,
                heatLevel,
                componentPlayer
            );
            if (adHocCraftingRecipe == null || !MatchRecipe(adHocCraftingRecipe.Ingredients, ingredients))
            {
                continue;
            }

            craftingRecipe = adHocCraftingRecipe;
            break;
        }

        if (craftingRecipe == null)
        {
            foreach (var recipe in ReadonlyRecipes.Where(recipe =>
                         !recipe.IsAdHocCraftingRecipe && MatchRecipe(recipe.Ingredients, ingredients)))
            {
                craftingRecipe = recipe;
                break;
            }
        }

        if (craftingRecipe != null)
        {
            if (heatLevel < craftingRecipe.RequiredHeatLevel)
            {
                craftingRecipe = !(heatLevel > 0f)
                    ? new CraftingRecipe
                    {
                        Message = LanguageManager.Get(_typeName, 0)
                    }
                    : new CraftingRecipe
                    {
                        Message = LanguageManager.Get(_typeName, 1)
                    };
            }
            else if (playerLevel < craftingRecipe.RequiredPlayerLevel)
            {
                craftingRecipe = !(craftingRecipe.RequiredHeatLevel > 0f)
                    ? new CraftingRecipe
                    {
                        Message = string.Format(LanguageManager.Get(_typeName, 2), craftingRecipe.RequiredPlayerLevel)
                    }
                    : new CraftingRecipe
                    {
                        Message = string.Format(LanguageManager.Get(_typeName, 3), craftingRecipe.RequiredPlayerLevel)
                    };
            }
        }

        if (craftingRecipe == null)
        {
            return null;
        }

        var subsystemGameInfo = terrain.Project.FindSubsystem<SubsystemGameInfo>(true)!;
        if (!subsystemGameInfo.WorldSettings.IsBlockDiable(craftingRecipe.ResultValue))
        {
            return craftingRecipe;
        }

        componentPlayer?.ComponentGui.DisplaySmallMessage("此物品已被禁用", Color.Red, false, true);
        return null;
    }

    public static int DecodeResult(string result)
    {
        return DecodeResult(result, true);
    }

    private static int DecodeResult(string result, bool useLegacyHooks)
    {
        if (string.IsNullOrEmpty(result))
        {
            return 0;
        }

        var array = result.Split([':'], StringSplitOptions.None);
        var block = BlocksManager.FindBlockByTypeName(array[0], true)!;
        return Terrain.MakeBlockValue(
            data: array.Length >= 2 ? int.Parse(array[1], CultureInfo.InvariantCulture) : 0,
            contents: block.BlockIndex, light: 0);
    }

    public static void DecodeIngredient(string ingredient, out string craftingId, out int? data)
    {
        DecodeIngredient(ingredient, true, out craftingId, out data);
    }

    private static void DecodeIngredient(
        string ingredient,
        bool useLegacyHooks,
        out string craftingId,
        out int? data)
    {
        var array = ingredient.Split([':'], StringSplitOptions.None);
        craftingId = array[0];
        data = array.Length >= 2 ? new int?(int.Parse(array[1], CultureInfo.InvariantCulture)) : null;
    }

    private static bool MatchRecipe(string[] requiredIngredients, string[] actualIngredients)
    {
        if (actualIngredients.Length > 9)
        {
            return false;
        }

        var array = new string[9];
        for (var i = 0; i < 2; i++)
        {
            for (var j = -3; j <= 3; j++)
            {
                for (var k = -3; k <= 3; k++)
                {
                    var flip = i != 0;
                    if (!TransformRecipe(array, requiredIngredients, k, j, flip))
                    {
                        continue;
                    }

                    var flag = true;
                    for (var l = 0; l < 9; l++)
                    {
                        if (!CompareIngredients(array[l], actualIngredients[l]))
                        {
                            flag = false;
                            break;
                        }
                    }

                    if (flag)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool TransformRecipe(string[] transformedIngredients, string[] ingredients, int shiftX, int shiftY,
        bool flip)
    {
        for (var i = 0; i < 9; i++)
        {
            transformedIngredients[i] = string.Empty;
        }

        for (var j = 0; j < 3; j++)
        {
            for (var k = 0; k < 3; k++)
            {
                var num = (flip ? 3 - k - 1 : k) + shiftX;
                var num2 = j + shiftY;
                var text = ingredients[k + j * 3];
                if (num >= 0 && num2 >= 0 && num < 3 && num2 < 3)
                {
                    transformedIngredients[num + num2 * 3] = text;
                }
                else if (!string.IsNullOrEmpty(text))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool CompareIngredients(string requiredIngredient, string actualIngredient)
    {
        if (string.IsNullOrEmpty(requiredIngredient))
        {
            return string.IsNullOrEmpty(actualIngredient);
        }

        if (string.IsNullOrEmpty(actualIngredient))
        {
            return string.IsNullOrEmpty(requiredIngredient);
        }

        DecodeIngredient(requiredIngredient, out var craftingId, out var data);
        DecodeIngredient(actualIngredient, out var craftingId2, out var data2);
        if (!data2.HasValue)
        {
            throw new InvalidOperationException("Actual ingredient data not specified.");
        }

        if (craftingId != craftingId2)
        {
            return false;
        }

        if (!data.HasValue)
        {
            return true;
        }

        return data.Value == data2.Value;
    }
}
