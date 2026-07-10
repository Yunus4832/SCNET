namespace Game.Blocks;

public class TerritoryBlock : CubeBlock
{
    public const int Index = 264;

    private const int _requiredPlayerLevel = 5;

    private string[] _needIngredients = [];

    public override void Initialize()
    {
        base.Initialize();
        CraftingId = "TerritoryBlock";
    }

    public static bool IsTerritoryValue(int value)
    {
        return Terrain.ExtractContents(value) == Index;
    }

    public override int GetFaceTextureSlot(int face, int value)
    {
        return 255;
    }

    public override IEnumerable<int> GetCreativeValues()
    {
        return [Terrain.MakeBlockValue(Index)];
    }

    public override BlockDigMethod GetBlockDigMethod(int value)
    {
        return BlockDigMethod.Quarry;
    }

    public override CraftingRecipe? GetAdHocCraftingRecipe(
        SubsystemTerrain subsystemTerrain,
        string[] ingredients,
        float heatLevel,
        ComponentPlayer? player
    )
    {
        if (player == null || !RecipeMatches(ingredients))
        {
            return null;
        }

        var userLevel = (int)player.PlayerData.Level;
        if (userLevel < _requiredPlayerLevel)
        {
            var displayName = GetDisplayName(subsystemTerrain, Terrain.MakeBlockValue(Index));
            player.ComponentGui.DisplaySmallMessage(
                string.Format(LanguageManager.Get(nameof(TerritoryBlock), 1), _requiredPlayerLevel, displayName,
                    userLevel),
                Color.Red,
                false,
                true
            );
            return null;
        }

        return new CraftingRecipe
        {
            ResultValue = Terrain.MakeBlockValue(Index),
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
                ResultValue = Terrain.MakeBlockValue(Index),
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

    public override BlockPlacementData GetPlacementValue(SubsystemTerrain subsystemTerrain,
        ComponentMiner componentMiner, int value, TerrainRaycastResult raycastResult)
    {
        return new BlockPlacementData { Value = value, CellFace = raycastResult.CellFace };
    }

    public override void GetDropValues(SubsystemTerrain subsystemTerrain, int oldValue, int newValue, int toolLevel,
        List<BlockDropValue> dropValues, out bool showDebris)
    {
        showDebris = DestructionDebrisScale > 0f;
        dropValues.Add(new BlockDropValue
        {
            Value = Terrain.MakeBlockValue(Index),
            Count = 1
        });
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
            BlocksManager.Blocks[CopperIngotBlock.Index].CraftingId + ":0",
            BlocksManager.Blocks[DiamondChunkBlock.Index].CraftingId + ":0",
            BlocksManager.Blocks[IronIngotBlock.Index].CraftingId + ":0",
            BlocksManager.Blocks[CopperIngotBlock.Index].CraftingId + ":0",
            BlocksManager.Blocks[DiamondChunkBlock.Index].CraftingId + ":0",
            BlocksManager.Blocks[IronIngotBlock.Index].CraftingId + ":0",
            BlocksManager.Blocks[GraniteBlock.Index].CraftingId + ":0",
            BlocksManager.Blocks[GraniteBlock.Index].CraftingId + ":0",
            BlocksManager.Blocks[GraniteBlock.Index].CraftingId + ":0",
        ];
    }
}
