using Engine.Graphics;

namespace Game.Blocks;

public class BoatBlock : Block
{
    public BlockMesh StandaloneBlockMesh = new();

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>("Models/BoatItem");
        var boatNode = model.FindNode("Boat")!;
        var nodeAbsoluteTransform = BlockMesh.GetNodeAbsoluteTransform(boatNode);
        StandaloneBlockMesh.AppendModelPrimitive(
            boatNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform * Matrix.CreateTranslation(0f, -0.4f, 0f),
            false,
            false,
            false,
            false,
            new Color(96, 96, 96)
        );
        StandaloneBlockMesh.AppendModelPrimitive(
            boatNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform * Matrix.CreateTranslation(0f, -0.4f, 0f),
            false,
            true,
            false,
            false,
            new Color(255, 255, 255)
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
            1f * size,
            ref matrix,
            environmentData
        );
    }
}
