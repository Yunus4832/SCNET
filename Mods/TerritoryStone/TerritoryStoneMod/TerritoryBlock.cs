using Engine.Core.Mathematics;

using Game.Blocks;
using Game.Crafting;
using Game.Modding.Blocks;
using Game.Protocol;
using Game.Subsystems;
using Game.Terrains;

namespace TerritoryStoneMod;

public class TerritoryBlock : CubeBlock
{
    public static ResourceId Id { get; } = new(new ModId(ModEntry.ModId), "territory_stone");

    private const int _requiredPlayerLevel = 5;

    private string[] _needIngredients = [];

    public override void Initialize()
    {
        base.Initialize();
        CraftingId = "TerritoryBlock";
    }

    public static bool IsTerritoryValue(int value, BlockContentCatalog catalog)
    {
        return Terrain.ExtractContents(value) == catalog.GetBlock(Id).BlockIndex;
    }

    public override bool IsEditable(int value) => true;

    public override IEnumerable<int> GetCreativeValues()
    {
        return [Terrain.MakeBlockValue(BlockIndex)];
    }

    public override BlockDigMethod GetBlockDigMethod(int value)
    {
        return BlockDigMethod.Quarry;
    }

    public override CraftingRecipe? GetAdHocCraftingRecipe(
        SubsystemTerrain subsystemTerrain,
        string[] ingredients,
        float heatLevel,
        float? playerLevel
    )
    {
        if (playerLevel is null || !RecipeMatches(ingredients))
        {
            return null;
        }

        return new CraftingRecipe
        {
            RequiredPlayerLevel = _requiredPlayerLevel,
            ResultValue = Terrain.MakeBlockValue(BlockIndex),
            ResultCount = 1,
            Ingredients = _needIngredients
        };
    }

    public override IEnumerable<CraftingRecipe> GetProceduralCraftingRecipes()
    {
        EnsureIngredients();

        return
        [
            new CraftingRecipe
            {
                ResultValue = Terrain.MakeBlockValue(BlockIndex),
                ResultCount = 1,
                Ingredients = _needIngredients,
                IsAdHocCraftingRecipe = true
            }
        ];
    }

    public override int GetMaxStacking(int value)
    {
        return 1;
    }

    public override float GetDigResilience(int value)
    {
        return 12f;
    }

    public override BlockPlacementData GetPlacementValue(
        SubsystemTerrain subsystemTerrain,
        Vector3 eyeForward,
        int value,
        TerrainRaycastResult raycastResult
    )
    {
        return new BlockPlacementData { Value = value, CellFace = raycastResult.CellFace };
    }

    public override void GetDropValues(
        SubsystemTerrain subsystemTerrain,
        int oldValue,
        int newValue,
        int toolLevel,
        List<BlockDropValue> dropValues,
        out bool showDebris
    )
    {
        showDebris = AllowsDestructionDebris(oldValue, newValue);
        dropValues.Add(
            new BlockDropValue
            {
                Value = Terrain.MakeBlockValue(BlockIndex),
                Count = 1
            }
        );
    }

    public override bool IsMovableByPiston(int value, int pistonFace, int y, out bool isEnd)
    {
        isEnd = false;
        return false;
    }

    private bool RecipeMatches(string[] ingredients)
    {
        EnsureIngredients();
        for (var i = 0; i < 9; i++)
        {
            if (ingredients[i] != _needIngredients[i])
            {
                return false;
            }
        }

        return true;
    }

    private void EnsureIngredients()
    {
        if (_needIngredients.Length != 0)
        {
            return;
        }

        _needIngredients =
        [
            Catalog.GetBlock(ResourceId.Parse("game:copper_ingot")).CraftingId + ":0",
            Catalog.GetBlock(ResourceId.Parse("game:diamond_chunk")).CraftingId + ":0",
            Catalog.GetBlock(ResourceId.Parse("game:iron_ingot")).CraftingId + ":0",
            Catalog.GetBlock(ResourceId.Parse("game:copper_ingot")).CraftingId + ":0",
            Catalog.GetBlock(ResourceId.Parse("game:diamond_chunk")).CraftingId + ":0",
            Catalog.GetBlock(ResourceId.Parse("game:iron_ingot")).CraftingId + ":0",
            Catalog.GetBlock(ResourceId.Parse("game:granite")).CraftingId + ":0",
            Catalog.GetBlock(ResourceId.Parse("game:granite")).CraftingId + ":0",
            Catalog.GetBlock(ResourceId.Parse("game:granite")).CraftingId + ":0",
        ];
    }
}
