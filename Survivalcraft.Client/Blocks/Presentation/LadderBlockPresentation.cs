using Engine.Graphics;

using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public abstract class LadderBlockPresentation(LadderBlock block, BlockPresentationCatalog catalog, string modelName)
    : BlockPresentation(block, catalog)
{
    protected new LadderBlock Block => (LadderBlock)base.Block;

    public readonly BlockMesh[] BlockMeshesByData = new BlockMesh[4];

    public readonly BlockMesh StandaloneBlockMesh = new();

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
                geometry.SubsetOpaque
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
            size,
            ref matrix,
            environmentData
        );
    }

    public string ModelName = modelName;

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>(ModelName);
        var ladderNode = model.FindNode("Ladder")!;
        var nodeAbsoluteTransform = BlockMesh.GetNodeAbsoluteTransform(ladderNode);
        for (var i = 0; i < 4; i++)
        {
            BlockMeshesByData[i] = new BlockMesh();
            var m = Matrix.CreateTranslation(0f, 0f, 0f - (0.5f - Block.Offset)) *
                    Matrix.CreateRotationY(i * (float)Math.PI / 2f) *
                    Matrix.CreateTranslation(0.5f, 0f, 0.5f);
            BlockMeshesByData[i].AppendModelPrimitive(
                ladderNode.RequireMesh().Primitives[0],
                nodeAbsoluteTransform * m,
                false,
                false,
                false,
                false,
                Color.White
            );
            BlockMeshesByData[i].GenerateSidesData();
        }

        StandaloneBlockMesh.AppendModelPrimitive(
            ladderNode.RequireMesh().Primitives[0],
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
