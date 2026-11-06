using Engine.Graphics;

using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class CactusBlockPresentation(CactusBlock block, BlockPresentationCatalog catalog)
    : BlockPresentation(block, catalog)
{
    protected new CactusBlock Block => (CactusBlock)base.Block;

    public BlockMesh BlockMesh = new();

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
        generator.GenerateMeshVertices(this, x, y, z, BlockMesh, Color.White, null, geometry.SubsetAlphaTest);
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
            size,
            ref matrix,
            environmentData
        );
    }

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>("Models/Cactus");
        var cactusNode = model.FindNode("Cactus")!;
        var nodeAbsoluteTransform = BlockMesh.GetNodeAbsoluteTransform(cactusNode);
        BlockMesh.AppendModelPrimitive(
            cactusNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform * Matrix.CreateTranslation(0.5f, 0f, 0.5f),
            false,
            false,
            false,
            false,
            Color.White
        );
        StandaloneBlockMesh.AppendModelPrimitive(
            cactusNode.RequireMesh().Primitives[0],
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
