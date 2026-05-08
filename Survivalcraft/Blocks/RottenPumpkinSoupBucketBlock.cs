using Engine.Graphics;

namespace Game.Blocks;

public class RottenPumpkinSoupBucketBlock : BucketBlock
{
    public const int Index = 252;

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
            Matrix.CreateTranslation(0f, -0.3f, 0f), false, false, false, false, new Color(255, 160, 64));
        StandaloneBlockMesh.TransformTextureCoordinates(Matrix.CreateTranslation(0f, 0.625f, 0f));
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
}
