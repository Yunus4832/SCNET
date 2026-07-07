using System.Globalization;

using Engine.Graphics;

namespace Game.Blocks;

public class PaintBucketBlock : BucketBlock
{
    public const int Index = 129;

    public readonly BlockMesh StandaloneBucketBlockMesh = new();

    public readonly BlockMesh StandalonePaintBlockMesh = new();

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>("Models/FullBucket");
        var bucketMesh = model.FindMesh("Bucket")!;
        var boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(
            bucketMesh.ParentBone ??
            throw new InvalidOperationException("Required BucketMesh.ParentBone is null")
        );
        var contentsMesh = model.FindMesh("Contents")!;
        var boneAbsoluteTransform2 = BlockMesh.GetBoneAbsoluteTransform(
            contentsMesh.ParentBone ??
            throw new InvalidOperationException("Required ContentsMesh.ParentBone is null")
        );
        StandaloneBucketBlockMesh.AppendModelMeshPart(bucketMesh.MeshParts[0],
            boneAbsoluteTransform * Matrix.CreateRotationY(MathUtils.DegToRad(180f)) *
            Matrix.CreateTranslation(0f, -0.3f, 0f), false, false, false, false, Color.White);
        StandalonePaintBlockMesh.AppendModelMeshPart(contentsMesh.MeshParts[0],
            boneAbsoluteTransform2 * Matrix.CreateRotationY(MathUtils.DegToRad(180f)) *
            Matrix.CreateTranslation(0f, -0.3f, 0f), false, false, false, false, Color.White);
        StandalonePaintBlockMesh.TransformTextureCoordinates(Matrix.CreateTranslation(0.9375f, 0f, 0f));
        base.Initialize();
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
        var color2 = GetColor(Terrain.ExtractData(value));
        BlocksManager.DrawMeshBlock(
            primitivesRenderer,
            StandaloneBucketBlockMesh,
            color,
            2f * size,
            ref matrix,
            environmentData
        );
        BlocksManager.DrawMeshBlock(
            primitivesRenderer,
            StandalonePaintBlockMesh,
            color * SubsystemPalette.GetColor(environmentData, color2),
            2f * size,
            ref matrix,
            environmentData
        );
    }

    public override IEnumerable<int> GetCreativeValues()
    {
        var i = 0;
        while (i < 16)
        {
            yield return Terrain.MakeBlockValue(129, 0, SetColor(0, i));
            var num = i + 1;
            i = num;
        }
    }

    public override IEnumerable<CraftingRecipe> GetProceduralCraftingRecipes()
    {
        var additives = new string[]
        {
            BlocksManager.Blocks[43].CraftingId,
            BlocksManager.Blocks[24].CraftingId,
            BlocksManager.Blocks[103].CraftingId,
            BlocksManager.Blocks[22].CraftingId
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
                        Description = $"制作 {SubsystemPalette.GetName(num)} 颜料",
                        ResultValue = Terrain.MakeBlockValue(129, 0, num),
                        ResultCount = 1,
                        RequiredHeatLevel = 1f,
                        Ingredients =
                        {
                            [0] = BlocksManager.Blocks[129].CraftingId + ":" +
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

    public override string GetDisplayName(SubsystemTerrain? subsystemTerrain, int value)
    {
        var color = GetColor(Terrain.ExtractData(value));
        return SubsystemPalette.GetName(color, LanguageManager.Get("BasePaintBucketBlock", 1));
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
