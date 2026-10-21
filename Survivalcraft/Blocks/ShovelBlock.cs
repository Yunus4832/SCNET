using Engine.Graphics;

namespace Game.Blocks;

public abstract class ShovelBlock(int handleTextureSlot, int headTextureSlot) : Block
{
    public int HandleTextureSlot = handleTextureSlot;

    public int HeadTextureSlot = headTextureSlot;

    public BlockMesh StandaloneBlockMesh = new();

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>("Models/Shovel");
        var handleNode = model.FindNode("Handle")!;
        var nodeAbsoluteTransform = BlockMesh.GetNodeAbsoluteTransform(handleNode);
        var headNode = model.FindNode("Head")!;
        var nodeAbsoluteTransform2 = BlockMesh.GetNodeAbsoluteTransform(headNode);
        var blockMesh = new BlockMesh();
        blockMesh.AppendModelPrimitive(
            handleNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform * Matrix.CreateTranslation(0f, -0.5f, 0f),
            false,
            false,
            false,
            false,
            Color.White
        );
        blockMesh.TransformTextureCoordinates(
            Matrix.CreateTranslation(
                HandleTextureSlot % 16 / 16f,
                HandleTextureSlot / 16 / 16f,
                0f
            )
        );
        var blockMesh2 = new BlockMesh();
        blockMesh2.AppendModelPrimitive(
            headNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform2 * Matrix.CreateTranslation(0f, -0.5f, 0f),
            false,
            false,
            false,
            false,
            Color.White
        );
        blockMesh2.TransformTextureCoordinates(
            Matrix.CreateTranslation(
                HeadTextureSlot % 16 / 16f,
                HeadTextureSlot / 16 / 16f,
                0f
            )
        );
        StandaloneBlockMesh.AppendBlockMesh(blockMesh);
        StandaloneBlockMesh.AppendBlockMesh(blockMesh2);
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
