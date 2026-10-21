using Engine.Graphics;

namespace Game.Blocks;

public class IncendiaryBombBlock : Block
{
    public BlockMesh StandaloneBlockMesh = new();

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>("Models/Bomb");
        var bombNode = model.FindNode("Bomb")!;
        var nodeAbsoluteTransform = BlockMesh.GetNodeAbsoluteTransform(bombNode);
        StandaloneBlockMesh.AppendModelPrimitive(
            bombNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform * Matrix.CreateTranslation(0f, -0.25f, 0f),
            false,
            false,
            false,
            false,
            new Color(0.4f, 0.2f, 0.2f)
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
