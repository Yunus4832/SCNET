using Engine.Graphics;

using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class BatteryBlockPresentation(BatteryBlock block, BlockPresentationCatalog catalog)
    : BlockPresentation(block, catalog)
{
    protected new BatteryBlock Block => (BatteryBlock)base.Block;

    public BlockMesh BlockMesh = new();

    public BlockMesh StandaloneBlockMesh = new();

    public override void GenerateTerrainVertices(
        BlockGeometryGenerator generator,
        TerrainGeometry geometry,
        int value,
        int x,
        int y,
        int z
    )
    {
        generator.GenerateMeshVertices(this, x, y, z, BlockMesh, Color.White, null, geometry.SubsetOpaque);
        generator.GenerateWireVertices(value, x, y, z, 4, 0.72f, Vector2.Zero, geometry.SubsetOpaque);
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
            1f * size,
            ref matrix,
            environmentData
        );
    }

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>("Models/Battery");
        var batteryNode = model.FindNode("Battery")!;
        var nodeAbsoluteTransform = BlockMesh.GetNodeAbsoluteTransform(batteryNode);
        StandaloneBlockMesh.AppendModelPrimitive(
            batteryNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform * Matrix.CreateTranslation(0f, -0.5f, 0f),
            false,
            false,
            false,
            false,
            Color.White
        );
        BlockMesh.AppendModelPrimitive(
            batteryNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform * Matrix.CreateTranslation(0.5f, 0f, 0.5f),
            false,
            false,
            false,
            false,
            Color.White
        );
    }
}
