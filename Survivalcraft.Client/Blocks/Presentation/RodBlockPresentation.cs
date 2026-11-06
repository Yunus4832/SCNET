using Engine.Graphics;

using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class RodBlockPresentation(RodBlock block, BlockPresentationCatalog catalog) : BlockPresentation(block, catalog)
{
    protected new RodBlock Block => (RodBlock)base.Block;

    public readonly BlockMesh StandaloneBlockMesh = new();

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
            2f * size,
            ref matrix,
            environmentData
        );
    }

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>("Models/Rod");
        var ironRodNode = model.FindNode("IronRod")!;
        var nodeAbsoluteTransform = BlockMesh.GetNodeAbsoluteTransform(ironRodNode);
        StandaloneBlockMesh.AppendModelPrimitive(
            ironRodNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform * Matrix.CreateTranslation(0f, -0.5f, 0f),
            false,
            false,
            false,
            false,
            Color.White
        );
        base.Initialize();
    }
}
