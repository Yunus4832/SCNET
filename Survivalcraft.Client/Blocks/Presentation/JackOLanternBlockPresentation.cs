using Engine.Graphics;

using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class JackOLanternBlockPresentation(JackOLanternBlock block, BlockPresentationCatalog catalog)
    : BlockPresentation(block, catalog)
{
    protected new JackOLanternBlock Block => (JackOLanternBlock)base.Block;

    public BlockMesh[] BlockMeshesByData = new BlockMesh[4];

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
        var num = Terrain.ExtractData(value);
        if (num < BlockMeshesByData.Length)
        {
            generator.GenerateMeshVertices(
                this,
                x,
                y,
                z,
                BlockMeshesByData[num],
                Color.White,
                null,
                geometry.SubsetAlphaTest
            );
        }
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
        var model = ContentManager.Get<Model>("Models/Pumpkins");
        var jackOLanternNode = model.FindNode("JackOLantern")!;
        var nodeAbsoluteTransform = BlockMesh.GetNodeAbsoluteTransform(jackOLanternNode);
        for (var i = 0; i < 4; i++)
        {
            var radians = i * (float)Math.PI / 2f;
            var blockMesh = new BlockMesh();
            blockMesh.AppendModelPrimitive(
                jackOLanternNode.RequireMesh().Primitives[0],
                nodeAbsoluteTransform * Matrix.CreateRotationY(radians) * Matrix.CreateTranslation(0.5f, 0f, 0.5f),
                false,
                false,
                false,
                false,
                new Color(232, 232, 232)
            );
            blockMesh.AppendModelPrimitive(
                jackOLanternNode.RequireMesh().Primitives[0],
                nodeAbsoluteTransform * Matrix.CreateRotationY(radians) * Matrix.CreateTranslation(0.5f, 0f, 0.5f),
                true,
                true,
                false,
                false,
                Color.White
            );
            BlockMeshesByData[i] = blockMesh;
        }

        StandaloneBlockMesh.AppendModelPrimitive(
            jackOLanternNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform * Matrix.CreateTranslation(0f, -0.23f, 0f),
            false,
            false,
            false,
            false,
            new Color(232, 232, 232)
        );
        StandaloneBlockMesh.AppendModelPrimitive(
            jackOLanternNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform * Matrix.CreateTranslation(0f, -0.23f, 0f),
            true,
            true,
            false,
            false,
            Color.White
        );
        base.Initialize();
    }
}
