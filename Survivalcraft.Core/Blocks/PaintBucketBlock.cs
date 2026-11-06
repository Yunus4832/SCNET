using System.Globalization;

namespace Game.Blocks;

public class PaintBucketBlock : BucketBlock
{
    public override IEnumerable<int> GetCreativeValues()
    {
        var i = 0;
        while (i < 16)
        {
            yield return Terrain.MakeBlockValue(BlockIndex, 0, SetColor(0, i));
            var num = i + 1;
            i = num;
        }
    }

    public override IEnumerable<CraftingRecipe> GetProceduralCraftingRecipes()
    {
        var additives = new[]
        {
            Catalog.Blocks[43].CraftingId,
            Catalog.Blocks[24].CraftingId,
            Catalog.Blocks[103].CraftingId,
            Catalog.Blocks[22].CraftingId
        };
        var color = 0;
        while (color < 16)
        {
            int num2;
            for (var additive = 0; additive < 4; additive = num2)
            {
                var num = CombineColors(color, 1 << additive);
                if (num != color)
                {
                    var craftingRecipe = new CraftingRecipe
                    {
                        Description = new LocalizedText(nameof(PaintBucketBlock), "CraftingPrefix", "Make"),
                        DescriptionColor = num,
                        DescriptionSuffix = new LocalizedText(nameof(PaintBucketBlock), "CraftingSuffix", "paint"),
                        ResultValue = Terrain.MakeBlockValue(BlockIndex, 0, num),
                        ResultCount = 1,
                        RequiredHeatLevel = 1f,
                        Ingredients =
                        {
                            [0] = Catalog.Blocks[BlockIndex].CraftingId +
                                  ":" +
                                  color.ToString(CultureInfo.InvariantCulture),
                            [1] = additives[additive]
                        }
                    };
                    yield return craftingRecipe;
                }

                num2 = additive + 1;
            }

            num2 = color + 1;
            color = num2;
        }
    }


    public override int GetDamageDestructionValue(int value)
    {
        return Terrain.MakeBlockValue(90);
    }

    public static int GetColor(int data)
    {
        return data & 0xF;
    }

    public static int SetColor(int data, int color)
    {
        return (data & -16) | (color & 0xF);
    }

    public static Vector4 ColorToCmyk(int color)
    {
        var num = color & 1;
        var num2 = (color >> 1) & 1;
        var num3 = (color >> 2) & 1;
        var num4 = (color >> 3) & 1;
        return new Vector4(num, num2, num3, num4);
    }

    public static int CmykToColor(Vector4 cmyk)
    {
        if (cmyk.W <= 1f)
        {
            var num = (int)MathUtils.Round(MathUtils.Saturate(cmyk.X));
            var num2 = (int)MathUtils.Round(MathUtils.Saturate(cmyk.Y));
            var num3 = (int)MathUtils.Round(MathUtils.Saturate(cmyk.Z));
            var num4 = (int)MathUtils.Round(MathUtils.Saturate(cmyk.W));
            return num | (num2 << 1) | (num3 << 2) | (num4 << 3);
        }

        return 15;
    }

    public static int CombineColors(int color1, int color2)
    {
        return CmykToColor(ColorToCmyk(color1) + ColorToCmyk(color2));
    }
}
