using System.Globalization;

namespace Game.Blocks;

public class PaintBucketBlock : BucketBlock
{
    public override IEnumerable<int> GetCreativeValues()
    {
        var i = 0;
        while (i < 16)
        {
            yield return Terrain.MakeBlockValue(BlockIndex, SetColor(0, i));
            var nextColor = i + 1;
            i = nextColor;
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
            int nextVariant;
            for (var additive = 0; additive < 4; additive = nextVariant)
            {
                var mixedColor = CombineColors(color, 1 << additive);
                if (mixedColor != color)
                {
                    var craftingRecipe = new CraftingRecipe
                    {
                        Description = new LocalizedText(nameof(PaintBucketBlock), "CraftingPrefix", "Make"),
                        DescriptionColor = mixedColor,
                        DescriptionSuffix = new LocalizedText(nameof(PaintBucketBlock), "CraftingSuffix", "paint"),
                        ResultValue = Terrain.MakeBlockValue(BlockIndex, mixedColor),
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

                nextVariant = additive + 1;
            }

            nextVariant = color + 1;
            color = nextVariant;
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
        var cyan = color & 1;
        var magenta = (color >> 1) & 1;
        var yellow = (color >> 2) & 1;
        var black = (color >> 3) & 1;
        return new Vector4(cyan, magenta, yellow, black);
    }

    public static int CmykToColor(Vector4 cmyk)
    {
        if (cmyk.W <= 1f)
        {
            var cyan = (int)MathUtils.Round(MathUtils.Saturate(cmyk.X));
            var magenta = (int)MathUtils.Round(MathUtils.Saturate(cmyk.Y));
            var yellow = (int)MathUtils.Round(MathUtils.Saturate(cmyk.Z));
            var black = (int)MathUtils.Round(MathUtils.Saturate(cmyk.W));
            return cyan | (magenta << 1) | (yellow << 2) | (black << 3);
        }

        return 15;
    }

    public static int CombineColors(int color1, int color2)
    {
        return CmykToColor(ColorToCmyk(color1) + ColorToCmyk(color2));
    }
}
