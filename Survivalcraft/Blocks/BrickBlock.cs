using Engine.Graphics;

namespace Game.Blocks;

public class BrickBlock : Block
{
    public BlockMesh StandaloneBlockMesh = new();

    public override bool FurnitureBuilt { get; set; } = true;

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
