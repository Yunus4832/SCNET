using Engine.Graphics;

using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class StoneClubBlockPresentation(StoneClubBlock block, BlockPresentationCatalog catalog)
    : BlockPresentation(block, catalog)
{
    protected new StoneClubBlock Block => (StoneClubBlock)base.Block;

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
            2f * size,
            ref matrix,
            environmentData
        );
    }

    public override void Initialize()
    {
        var handleTextureSlot = 47;
        var headTextureSlot = 1;
        var model = AssetManager.Get<Model>("Models/StoneClub");
        var handleNode = model.FindNode("Handle")!;
        var nodeAbsoluteTransform = BlockMesh.GetNodeAbsoluteTransform(handleNode);
        var spikesNode = model.FindNode("Spikes")!;
        var nodeAbsoluteTransform2 = BlockMesh.GetNodeAbsoluteTransform(spikesNode);
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
            Matrix.CreateTranslation(handleTextureSlot % 16 / 16f, handleTextureSlot / 16 / 16f, 0f)
        );
        var blockMesh2 = new BlockMesh();
        blockMesh2.AppendModelPrimitive(
            spikesNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform2 * Matrix.CreateTranslation(0f, -0.5f, 0f),
            false,
            false,
            false,
            false,
            Color.White
        );
        blockMesh2.TransformTextureCoordinates(
            Matrix.CreateTranslation(headTextureSlot % 16 / 16f, headTextureSlot / 16 / 16f, 0f)
        );
        StandaloneBlockMesh.AppendBlockMesh(blockMesh);
        StandaloneBlockMesh.AppendBlockMesh(blockMesh2);
        base.Initialize();
    }
}
