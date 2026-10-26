using System.Globalization;

using Engine.Graphics;

using Game.Crafting;

namespace Game.Blocks;

public class PumpkinSoupBucketBlock : BucketBlock
{
    public BlockMesh StandaloneBlockMesh = new();

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>("Models/FullBucket");
        var bucketNode = model.FindNode("Bucket")!;
        var nodeAbsoluteTransform = BlockMesh.GetNodeAbsoluteTransform(bucketNode);
        var contentsNode = model.FindNode("Contents")!;
        var nodeAbsoluteTransform2 = BlockMesh.GetNodeAbsoluteTransform(contentsNode);
        StandaloneBlockMesh.AppendModelPrimitive(
            contentsNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform2 *
            Matrix.CreateRotationY(MathUtils.DegToRad(180f)) *
            Matrix.CreateTranslation(0f, -0.3f, 0f),
            false,
            false,
            false,
            false,
            new Color(200, 130, 35)
        );
        StandaloneBlockMesh.TransformTextureCoordinates(Matrix.CreateTranslation(0.0625f, 0.4375f, 0f));
        StandaloneBlockMesh.AppendModelPrimitive(
            bucketNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform *
            Matrix.CreateRotationY(MathUtils.DegToRad(180f)) *
            Matrix.CreateTranslation(0f, -0.3f, 0f),
            false,
            false,
            false,
            false,
            Color.White
        );
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
                    ResultValue = BlockIndex,
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
