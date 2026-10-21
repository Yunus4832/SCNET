using Engine.Graphics;

namespace Game.Blocks;

public class SnowballBlock : Block
{
    public BlockMesh StandaloneBlockMesh = new();

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>("Models/Snowball");
        var snowballNode = model.FindNode("Snowball")!;
        var nodeAbsoluteTransform = BlockMesh.GetNodeAbsoluteTransform(snowballNode);
        StandaloneBlockMesh.AppendModelPrimitive(
            snowballNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform * Matrix.CreateTranslation(0f, 0f, 0f),
            false,
            false,
            false,
            false,
            Color.White
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
            2.5f * size,
            ref matrix,
            environmentData
        );
    }
}
