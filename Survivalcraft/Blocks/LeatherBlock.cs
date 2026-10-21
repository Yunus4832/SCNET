using Engine.Graphics;

namespace Game.Blocks;

public class LeatherBlock : Block
{
    public readonly BlockMesh StandaloneBlockMesh = new();

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>("Models/Leather");
        var leatherNode = model.FindNode("Leather")!;
        var nodeAbsoluteTransform = BlockMesh.GetNodeAbsoluteTransform(leatherNode);
        StandaloneBlockMesh.AppendModelPrimitive(
            leatherNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform * Matrix.CreateTranslation(0f, 0f, 0f),
            false,
            false,
            false,
            false,
            Color.White
        );
        StandaloneBlockMesh.AppendModelPrimitive(
            leatherNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform * Matrix.CreateTranslation(0f, 0f, 0f),
            false,
            true,
            false,
            false,
            new Color(128, 128, 160)
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
