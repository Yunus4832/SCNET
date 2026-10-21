using Engine.Graphics;

namespace Game.Blocks;

public class WaterBucketBlock : BucketBlock
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
            new Color(32, 80, 224, 255)
        );
        StandaloneBlockMesh.TransformTextureCoordinates(Matrix.CreateTranslation(0.8125f, 0.6875f, 0f));
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
}
