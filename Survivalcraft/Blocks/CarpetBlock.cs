using Engine.Graphics;

namespace Game.Blocks;

public class CarpetBlock : CubeBlock
{
    public const int Index = 208;

    public readonly BoundingBox[] CollisionBoxes = [new(new Vector3(0f, 0f, 0f), new Vector3(1f, 0.0625f, 1f))];

    public override bool FurnitureBuilt { get; set; } = true;

    public override bool IsFaceTransparent(SubsystemTerrain subsystemTerrain, int face, int value)
    {
        return face != 5;
    }

    public override void GenerateTerrainVertices(
        BlockGeometryGenerator generator,
        TerrainGeometry geometry,
        int value,
        int x,
        int y,
        int z
    )
    {
        var data = Terrain.ExtractData(value);
        var fabricColor = SubsystemPalette.GetFabricColor(generator, GetColor(data));
        generator.GenerateCubeVertices(this, value, x, y, z, 0.0625f, 0.0625f, 0.0625f, 0.0625f, fabricColor,
            fabricColor, fabricColor, fabricColor, fabricColor, -1, geometry.OpaqueSubsetsByFace);
    }

    public override void DrawBlock(
        PrimitivesRenderer3D primitivesRenderer,
        int value,
        Color color,
        float size,
        ref Matrix matrix,
        DrawBlockEnvironmentData environmentData
    )
    {
        var data = Terrain.ExtractData(value);
        color *= SubsystemPalette.GetFabricColor(environmentData, GetColor(data));
        BlocksManager.DrawCubeBlock(
            primitivesRenderer,
            value,
            new Vector3(size, 0.0625f * size, size),
            ref matrix,
            color,
            color,
            environmentData
        );
    }

    public override IEnumerable<int> GetCreativeValues()
    {
        var i = 0;
        while (i < 16)
        {
            yield return Terrain.MakeBlockValue(208, 0, i);
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
        dropValues.Add(new BlockDropValue
        {
            Value = Terrain.MakeBlockValue(208, 0, data),
            Count = 1
        });
    }

    public override BlockDebrisParticleSystem CreateDebrisParticleSystem(
        SubsystemTerrain subsystemTerrain,
        Vector3 position,
        int value,
        float strength
    )
    {
        var data = Terrain.ExtractData(value);
        var fabricColor = SubsystemPalette.GetFabricColor(subsystemTerrain, GetColor(data));
        return new BlockDebrisParticleSystem(subsystemTerrain, position, strength, DestructionDebrisScale, fabricColor,
            TextureSlot);
    }

    public override CraftingRecipe? GetAdHocCraftingRecipe(
        SubsystemTerrain subsystemTerrain,
        string[] ingredients,
        float heatLevel,
        ComponentPlayer? player
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
            CraftingRecipesManager.DecodeIngredient(item, out var craftingId, out var data);
            if (craftingId == BlocksManager.Blocks[208].CraftingId)
            {
                num3 = Terrain.MakeBlockValue(208, 0, data ?? 0);
            }
            else if (craftingId == BlocksManager.Blocks[129].CraftingId)
            {
                num = Terrain.MakeBlockValue(129, 0, data ?? 0);
            }
            else if (craftingId == BlocksManager.Blocks[128].CraftingId)
            {
                num2 = Terrain.MakeBlockValue(128, 0, data ?? 0);
            }
        }

        if (num != 0 && num3 != 0)
        {
            var num4 = Terrain.ExtractData(num3);
            var color = PaintBucketBlock.GetColor(Terrain.ExtractData(num));
            var damage = BlocksManager.Blocks[129].GetDamage(num);
            var block = BlocksManager.Blocks[129];
            var num5 = PaintBucketBlock.CombineColors(num4, color);
            if (num5 != num4)
            {
                return new CraftingRecipe
                {
                    ResultCount = 1,
                    ResultValue = Terrain.MakeBlockValue(208, 0, num5),
                    RemainsCount = 1,
                    RemainsValue = BlocksManager.DamageItem(Terrain.MakeBlockValue(129, 0, color),
                        damage + MathUtils.Max(block.Durability / 4, 1)),
                    RequiredHeatLevel = 1f,
                    Description = $"Dye carpet {SubsystemPalette.GetName(color, string.Empty)}",
                    Ingredients = (string[])ingredients.Clone()
                };
            }
        }

        if (num2 == 0 || num3 == 0)
        {
            return null;
        }

        var num6 = Terrain.ExtractData(num3);
        var damage2 = BlocksManager.Blocks[128].GetDamage(num2);
        var block2 = BlocksManager.Blocks[128];
        if (num6 != 0)
        {
            return new CraftingRecipe
            {
                ResultCount = 1,
                ResultValue = Terrain.MakeBlockValue(208, 0, 0),
                RemainsCount = 1,
                RemainsValue = BlocksManager.DamageItem(Terrain.MakeBlockValue(128, 0, 0),
                    damage2 + MathUtils.Max(block2.Durability / 4, 1)),
                RequiredHeatLevel = 1f,
                Description = "Undye carpet",
                Ingredients = (string[])ingredients.Clone()
            };
        }

        return null;
    }

    public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
    {
        return CollisionBoxes;
    }

    public override string GetDisplayName(SubsystemTerrain? subsystemTerrain, int value)
    {
        var value2 = Terrain.ExtractData(value);
        return SubsystemPalette.GetName(value2, "ตุฬบ");
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
