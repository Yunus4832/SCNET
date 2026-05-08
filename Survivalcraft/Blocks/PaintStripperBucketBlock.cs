using Engine.Graphics;

namespace Game.Blocks;

public class PaintStripperBucketBlock : BucketBlock
{
    public const int Index = 128;

    public BlockMesh StandaloneBlockMesh = new();

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>("Models/FullBucket");
        var bucketMesh = model.FindMesh("Bucket")!;
        var boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(
            bucketMesh.ParentBone ??
            throw new InvalidOperationException("Required BucketMesh.ParentBone is null")
        );
        var contensMesh = model.FindMesh("Contents")!;
        var boneAbsoluteTransform2 = BlockMesh.GetBoneAbsoluteTransform(
            contensMesh.ParentBone ??
            throw new InvalidOperationException("Required ContentsMesh.ParentBone is null")
        );
        StandaloneBlockMesh.AppendModelMeshPart(contensMesh.MeshParts[0],
            boneAbsoluteTransform2 * Matrix.CreateRotationY(MathUtils.DegToRad(180f)) *
            Matrix.CreateTranslation(0f, -0.3f, 0f), false, false, false, false, Color.White);
        StandaloneBlockMesh.TransformTextureCoordinates(Matrix.CreateTranslation(0.8125f, 0.6875f, 0f));
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
        return Terrain.MakeBlockValue(90);
    }
}
