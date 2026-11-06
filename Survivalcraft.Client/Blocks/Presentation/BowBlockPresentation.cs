using Engine.Graphics;

using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class BowBlockPresentation(BowBlock block, BlockPresentationCatalog catalog) : BlockPresentation(block, catalog)
{
    protected new BowBlock Block => (BowBlock)base.Block;

    public BlockMesh[] StandaloneBlockMeshes = new BlockMesh[16];

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
        var data = Terrain.ExtractData(value);
        var draw = BowBlock.GetDraw(data);
        var arrowType = BowBlock.GetArrowType(data);
        BlocksManager.DrawMeshBlock(
            primitivesRenderer,
            StandaloneBlockMeshes[draw],
            color,
            2f * size,
            ref matrix,
            environmentData
        );
        if (arrowType.HasValue)
        {
            var num = MathUtils.Lerp(0.14f, 0.68f, draw / 15f);
            var matrix2 = Matrix.CreateRotationX(-(float)Math.PI / 2f) *
                          Matrix.CreateTranslation(0f, 0.4f * size, (-1f + 2f * num) * size) *
                          matrix;
            var value2 = Terrain.MakeBlockValue(192, 0, ArrowBlock.SetArrowType(0, arrowType.Value));
            Catalog.Get(192).DrawBlock(primitivesRenderer, value2, color, size, ref matrix2, environmentData);
        }
    }

    public override bool IsSwapAnimationNeeded(int oldValue, int newValue)
    {
        var num = Terrain.ExtractContents(oldValue);
        var data = Terrain.ExtractData(oldValue);
        var data2 = Terrain.ExtractData(newValue);
        if (num == 191 && BowBlock.GetArrowType(data) == BowBlock.GetArrowType(data2))
        {
            return false;
        }

        return true;
    }

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>("Models/Bows");
        var bowRelaxedNode = model.FindNode("BowRelaxed")!;
        var stringRelaxedNode = model.FindNode("StringRelaxed")!;
        var bowTensedNode = model.FindNode("BowTensed")!;
        var stringTensedNode = model.FindNode("StringTensed")!;
        var nodeAbsoluteTransform = BlockMesh.GetNodeAbsoluteTransform(bowRelaxedNode);
        var nodeAbsoluteTransform2 = BlockMesh.GetNodeAbsoluteTransform(stringRelaxedNode);
        var nodeAbsoluteTransform3 = BlockMesh.GetNodeAbsoluteTransform(bowTensedNode);
        var nodeAbsoluteTransform4 = BlockMesh.GetNodeAbsoluteTransform(stringTensedNode);
        var blockMesh = new BlockMesh();
        blockMesh.AppendModelPrimitive(
            bowRelaxedNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform * Matrix.CreateTranslation(0f, -0.5f, 0f),
            false,
            false,
            false,
            false,
            Color.White
        );
        blockMesh.AppendModelPrimitive(
            stringRelaxedNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform2 * Matrix.CreateTranslation(0f, -0.5f, 0f),
            false,
            false,
            false,
            false,
            Color.White
        );
        var blockMesh2 = new BlockMesh();
        blockMesh2.AppendModelPrimitive(
            bowTensedNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform3 * Matrix.CreateTranslation(0f, -0.5f, 0f),
            false,
            false,
            false,
            false,
            Color.White
        );
        blockMesh2.AppendModelPrimitive(
            stringTensedNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform4 * Matrix.CreateTranslation(0f, -0.5f, 0f),
            false,
            false,
            false,
            false,
            Color.White
        );
        for (var i = 0; i < 16; i++)
        {
            var factor = i / 15f;
            StandaloneBlockMeshes[i] = new BlockMesh();
            StandaloneBlockMeshes[i].AppendBlockMesh(blockMesh);
            StandaloneBlockMeshes[i].BlendBlockMesh(blockMesh2, factor);
        }

        base.Initialize();
    }
}
