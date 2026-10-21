using Engine.Graphics;

namespace Game.Blocks;

public class SaddleBlock : Block
{
    public BlockMesh StandaloneBlockMesh = new();

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>("Models/Saddle");
        var saddleNode = model.FindNode("Saddle")!;
        var nodeAbsoluteTransform = BlockMesh.GetNodeAbsoluteTransform(saddleNode);
        StandaloneBlockMesh.AppendModelPrimitive(
            saddleNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform * Matrix.CreateTranslation(0f, -0.2f, 0f),
            false,
            false,
            false,
            false,
            new Color(224, 224, 224)
        );
        StandaloneBlockMesh.AppendModelPrimitive(
            saddleNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform * Matrix.CreateTranslation(0f, -0.2f, 0f),
            false,
            true,
            false,
            false,
            new Color(96, 96, 96)
        );
        base.Initialize();
    }

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
}
