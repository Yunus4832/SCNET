using Engine.Graphics;

using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class BrickBlockPresentation(BrickBlock block, BlockPresentationCatalog catalog)
    : BlockPresentation(block, catalog)
{
    protected new BrickBlock Block => (BrickBlock)base.Block;

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
            2.5f * size,
            ref matrix,
            environmentData
        );
    }

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>("Models/Brick");
        var brickNode = model.FindNode("Brick")!;
        var nodeAbsoluteTransform = BlockMesh.GetNodeAbsoluteTransform(brickNode);
        StandaloneBlockMesh.AppendModelPrimitive(
            brickNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform * Matrix.CreateTranslation(0f, -0.075f, 0f),
            false,
            false,
            false,
            false,
            Color.White
        );
        base.Initialize();
    }
}
