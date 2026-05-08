using Engine.Graphics;

namespace Game.Blocks;

public class EmptyBucketBlock : BucketBlock
{
    public const int Index = 90;

    public BlockMesh StandaloneBlockMesh = new();

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>("Models/EmptyBucket");
        var bucketMesh = model.FindMesh("Bucket")!;
        var boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(
            bucketMesh.ParentBone ??
            throw new InvalidOperationException("Required BucketMesh.ParentBone is null")
        );
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
