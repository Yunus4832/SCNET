namespace Game.Blocks;

public class BedrockBlock : CubeBlock
{
    public const int Index = 1;

    private string[] _needIngredients = [];

    public override void Initialize()
    {
        base.Initialize();
        Placeable = true;
        CraftingId = "BedrockBlock";
    }

    public override int GetFaceTextureSlot(int face, int value)
    {
        var id = Terrain.ExtractData(value);
        return id switch
        {
            1 => 255,
            _ => base.GetFaceTextureSlot(face, value)
        };
    }

    public override IEnumerable<int> GetCreativeValues()
    {
        return [Terrain.MakeBlockValue(Index, 0, 1)];
    }

    public override BlockDigMethod GetBlockDigMethod(int value)
    {
        var id = Terrain.ExtractData(value);
        return id switch
        {
            1 => BlockDigMethod.Quarry,
            _ => BlockDigMethod.None
        };
    }

    public override CraftingRecipe? GetAdHocCraftingRecipe(
        SubsystemTerrain subsystemTerrain,
        string[] ingredients,
        float heatLevel,
        ComponentPlayer? player
    )
    {
        if (player == null)
        {
            return null;
        }

        const int needLevel = 5;
        var userLevel = (int)player.PlayerData.Level;
        var isRecipeMatch = true;
        for (var i = 0; i < 9; i++)
        {
            if (ingredients[i] == _needIngredients[i])
            {
                continue;
            }

            isRecipeMatch = false;
            break;
        }

        if (!isRecipeMatch)
        {
            return null;
        }

        if (userLevel < needLevel)
        {
            player.ComponentGui.DisplaySmallMessage(
                $"需要等级大于或等于{needLevel}级才能制作领地石，当前等级{userLevel}级",
                Color.Red,
                false,
                true
            );
            return null;
        }

        return new CraftingRecipe
        {
            ResultValue = Terrain.MakeBlockValue(Index, 0, 1),
            ResultCount = 1,
            Ingredients = _needIngredients
        };
    }

    public override IEnumerable<CraftingRecipe> GetProceduralCraftingRecipes()
    {
        if (_needIngredients.Length == 0)
        {
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

        return
        [
            new CraftingRecipe
            {
                ResultValue = Terrain.MakeBlockValue(Index, 0, 1), ResultCount = 1, Ingredients = _needIngredients,
                IsAdHocCraftingRecipe = true
            }
        ];
    }

    public override int GetMaxStacking(int value)
    {
        var id = Terrain.ExtractData(value);
        switch (id)
        {
            case 1:
            {
                return 1;
            }
            default: return MaxStacking;
        }
    }

    public override float GetDigResilience(int value)
    {
        var id = Terrain.ExtractData(value);
        switch (id)
        {
            case 1:
            {
                return 12f;
            }
            default: return float.PositiveInfinity;
        }
    }

    public override string GetDisplayName(SubsystemTerrain? subsystemTerrain, int value)
    {
        var id = Terrain.ExtractData(value);
        return id switch
        {
            1 => "领地石",
            _ => base.GetDisplayName(subsystemTerrain, value)
        };
    }

    public override BlockPlacementData GetPlacementValue(SubsystemTerrain subsystemTerrain,
        ComponentMiner componentMiner, int value, TerrainRaycastResult raycastResult)
    {
        var id = Terrain.ExtractData(value);
        switch (id)
        {
            case 1:
            {
                return new BlockPlacementData { Value = value, CellFace = raycastResult.CellFace };
            }
            default: return base.GetPlacementValue(subsystemTerrain, componentMiner, value, raycastResult);
        }
    }

    public override void GetDropValues(SubsystemTerrain subsystemTerrain, int oldValue, int newValue, int toolLevel,
        List<BlockDropValue> dropValues, out bool showDebris)
    {
        base.GetDropValues(subsystemTerrain, oldValue, newValue, toolLevel, dropValues, out showDebris);
        if (oldValue == Terrain.MakeBlockValue(Index, 0, 1))
        {
            dropValues.Clear();
        }
    }

    public override string GetDescription(int value)
    {
        var id = Terrain.ExtractData(value);
        switch (id)
        {
            case 1:
            {
                return "放置后可拥有该方块周围1x1区块的挖掘放置权限，每个玩家仅能拥有一个领地石，若放置多个，以最后放置的为准.";
            }
            default: return base.GetDescription(value);
        }
    }

    public override bool IsMovableByPiston(int value, int pistonFace, int y, out bool isEnd)
    {
        isEnd = false;
        return y > 1;
    }
}
