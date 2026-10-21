using Engine.Graphics;

namespace Game.Blocks;

public class WhistleBlock : Block
{
    public BlockMesh StandaloneBlockMesh = new();

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>("Models/Whistle");
        var whistleNode = model.FindNode("Whistle")!;
        var nodeAbsoluteTransform = BlockMesh.GetNodeAbsoluteTransform(whistleNode);
        StandaloneBlockMesh.AppendModelPrimitive(
            whistleNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform * Matrix.CreateTranslation(0f, -0.04f, 0f),
            false,
            false,
            false,
            false,
            new Color(255, 255, 255)
        );
        StandaloneBlockMesh.AppendModelPrimitive(
            whistleNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform * Matrix.CreateTranslation(0f, -0.04f, 0f),
            false,
            true,
            false,
            false,
            new Color(64, 64, 64)
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
            9f * size,
            ref matrix,
            environmentData
        );
    }
}
