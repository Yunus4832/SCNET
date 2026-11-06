namespace Game.Blocks;

public class CarpetBlock : CubeBlock
{
    public readonly BoundingBox[] CollisionBoxes = [new(new Vector3(0f, 0f, 0f), new Vector3(1f, 0.0625f, 1f))];

    public override bool FurnitureBuilt { get; set; } = true;

    public override bool IsFaceTransparent(SubsystemTerrain subsystemTerrain, int face, int value)
    {
        return face != 5;
    }


    public override IEnumerable<int> GetCreativeValues()
    {
        var i = 0;
        while (i < 16)
        {
            yield return Terrain.MakeBlockValue(BlockIndex, 0, i);
            var num = i + 1;
            i = num;
        }
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
        showDebris = true;
        if (toolLevel < RequiredToolLevel)
        {
            return;
        }

        var data = Terrain.ExtractData(oldValue);
        dropValues.Add(
            new BlockDropValue
            {
                Value = Terrain.MakeBlockValue(BlockIndex, 0, data),
                Count = 1
            }
        );
    }


    public override CraftingRecipe? GetAdHocCraftingRecipe(
        SubsystemTerrain subsystemTerrain,
        string[] ingredients,
        float heatLevel,
        float? playerLevel
    )
    {
        if (heatLevel < 1f)
        {
            return null;
        }

        var list = ingredients.Where(i => !string.IsNullOrEmpty(i)).ToList();
        if (list.Count != 2)
        {
            return null;
        }

        var num = 0;
        var num2 = 0;
        var num3 = 0;
        foreach (var item in list)
        {
            CraftingRecipeCatalog.DecodeIngredient(item, out var craftingId, out var data);
            if (craftingId == Catalog.Blocks[BlockIndex].CraftingId)
            {
                num3 = Terrain.MakeBlockValue(BlockIndex, 0, data ?? 0);
            }
            else if (craftingId == Catalog.Blocks[129].CraftingId)
            {
                num = Terrain.MakeBlockValue(129, 0, data ?? 0);
            }
            else if (craftingId == Catalog.Blocks[128].CraftingId)
            {
                num2 = Terrain.MakeBlockValue(128, 0, data ?? 0);
            }
        }

        if (num != 0 && num3 != 0)
        {
            var num4 = Terrain.ExtractData(num3);
            var color = PaintBucketBlock.GetColor(Terrain.ExtractData(num));
            var damage = Catalog.Blocks[129].GetDamage(num);
            var block = Catalog.Blocks[129];
            var num5 = PaintBucketBlock.CombineColors(num4, color);
            if (num5 != num4)
            {
                return new CraftingRecipe
                {
                    ResultCount = 1,
                    ResultValue = Terrain.MakeBlockValue(BlockIndex, 0, num5),
                    RemainsCount = 1,
                    RemainsValue = Catalog.DamageItem(
                        Terrain.MakeBlockValue(129, 0, color),
                        damage + MathUtils.Max(block.Durability / 4, 1)
                    ),
                    RequiredHeatLevel = 1f,
                    Description = LocalizedText.Literal("Dye carpet"),
                    DescriptionColor = color,
                    Ingredients = (string[])ingredients.Clone()
                };
            }
        }

        if (num2 == 0 || num3 == 0)
        {
            return null;
        }

        var num6 = Terrain.ExtractData(num3);
        var damage2 = Catalog.Blocks[128].GetDamage(num2);
        var block2 = Catalog.Blocks[128];
        if (num6 != 0)
        {
            return new CraftingRecipe
            {
                ResultCount = 1,
                ResultValue = Terrain.MakeBlockValue(BlockIndex, 0, 0),
                RemainsCount = 1,
                RemainsValue = Catalog.DamageItem(
                    Terrain.MakeBlockValue(128, 0, 0),
                    damage2 + MathUtils.Max(block2.Durability / 4, 1)
                ),
                RequiredHeatLevel = 1f,
                Description = LocalizedText.Literal("Undye carpet"),
                Ingredients = (string[])ingredients.Clone()
            };
        }

        return null;
    }

    public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
    {
        return CollisionBoxes;
    }


    public static int GetColor(int data)
    {
        return data & 0xF;
    }

    public static int SetColor(int data, int color)
    {
        return (data & -16) | (color & 0xF);
    }
}
