using Engine.Graphics;

using Game.Modding.Blocks;
using Game.World.Presentation;

namespace Game.Blocks.Presentation;

public class PaintBucketBlockPresentation(PaintBucketBlock block, BlockPresentationCatalog catalog)
    : BucketBlockPresentation(block, catalog)
{
    protected new PaintBucketBlock Block => (PaintBucketBlock)base.Block;

    public readonly BlockMesh StandaloneBucketBlockMesh = new();

    public readonly BlockMesh StandalonePaintBlockMesh = new();

    public override void DrawBlock(
        PrimitivesRenderer3D primitivesRenderer,
        int value,
        Color color,
        float size,
        ref Matrix matrix,
        DrawBlockEnvironmentData environmentData
    )
    {
        var color2 = PaintBucketBlock.GetColor(Terrain.ExtractData(value));
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
            color * PalettePresentation.GetColor(environmentData.SubsystemTerrain, color2),
            2f * size,
            ref matrix,
            environmentData
        );
    }

    public override string GetDisplayName(SubsystemTerrain? subsystemTerrain, int value)
    {
        var color = PaintBucketBlock.GetColor(Terrain.ExtractData(value));
        return PalettePresentation.GetName(
            color,
            Localization.ClientLanguage.Catalog.Get("BasePaintBucketBlock", 1)
        );
    }

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>("Models/FullBucket");
        var bucketNode = model.FindNode("Bucket")!;
        var nodeAbsoluteTransform = BlockMesh.GetNodeAbsoluteTransform(bucketNode);
        var contentsNode = model.FindNode("Contents")!;
        var nodeAbsoluteTransform2 = BlockMesh.GetNodeAbsoluteTransform(contentsNode);
        StandaloneBucketBlockMesh.AppendModelPrimitive(
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
        StandalonePaintBlockMesh.AppendModelPrimitive(
            contentsNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform2 *
            Matrix.CreateRotationY(MathUtils.DegToRad(180f)) *
            Matrix.CreateTranslation(0f, -0.3f, 0f),
            false,
            false,
            false,
            false,
            Color.White
        );
        StandalonePaintBlockMesh.TransformTextureCoordinates(Matrix.CreateTranslation(0.9375f, 0f, 0f));
        base.Initialize();
    }
}
