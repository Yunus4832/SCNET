using System.Globalization;

using Engine.Graphics;

namespace Game.Blocks;

public class PumpkinSoupBucketBlock : BucketBlock
{
    public const int Index = 251;

    public BlockMesh StandaloneBlockMesh = new();

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
        StandaloneBlockMesh.AppendModelMeshPart(contentsMesh.MeshParts[0],
            boneAbsoluteTransform2 * Matrix.CreateRotationY(MathUtils.DegToRad(180f)) *
            Matrix.CreateTranslation(0f, -0.3f, 0f), false, false, false, false, new Color(200, 130, 35));
        StandaloneBlockMesh.TransformTextureCoordinates(Matrix.CreateTranslation(0.0625f, 0.4375f, 0f));
        StandaloneBlockMesh.AppendModelMeshPart(bucketMesh.MeshParts[0],
            boneAbsoluteTransform * Matrix.CreateRotationY(MathUtils.DegToRad(180f)) *
            Matrix.CreateTranslation(0f, -0.3f, 0f), false, false, false, false, Color.White);
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
        BlocksManager.DrawMeshBlock(
            primitivesRenderer,
            StandaloneBlockMesh,
            color,
            2f * size,
            ref matrix,
            environmentData
        );
    }

    public override int GetDamageDestructionValue(int value)
    {
        return 252;
    }

    public override IEnumerable<CraftingRecipe> GetProceduralCraftingRecipes()
    {
        var isDead = 0;
        while (isDead <= 1)
        {
            int num;
            for (var rot = 0; rot <= 1; rot = num)
            {
                var craftingRecipe = new CraftingRecipe
                {
                    ResultCount = 1,
                    ResultValue = 251,
                    RequiredHeatLevel = 1f,
                    Description = "烹饪南瓜粥"
                };
                var data = BasePumpkinBlock.SetIsDead(BasePumpkinBlock.SetSize(0, 7), isDead != 0);
                var value = SetDamage(Terrain.MakeBlockValue(131, 0, data), rot);
                craftingRecipe.Ingredients[0] =
                    "pumpkin:" + Terrain.ExtractData(value).ToString(CultureInfo.InvariantCulture);
                craftingRecipe.Ingredients[1] = "waterbucket";
                yield return craftingRecipe;
                num = rot + 1;
            }

            num = isDead + 1;
            isDead = num;
        }
    }
}
