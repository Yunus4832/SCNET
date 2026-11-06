using Engine.Graphics;

using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class CairnBlockPresentation(CairnBlock block, BlockPresentationCatalog catalog)
    : BlockPresentation(block, catalog)
{
    protected new CairnBlock Block => (CairnBlock)base.Block;

    private readonly BlockMesh _blockMesh = new();

    private readonly BlockMesh _standaloneMesh = new();

    public override void GenerateTerrainVertices(
        BlockGeometryGenerator generator,
        TerrainGeometry geometry,
        int value,
        int x,
        int y,
        int z
    )
    {
        generator.GenerateMeshVertices(this, x, y, z, _blockMesh, Color.White, null, geometry.SubsetOpaque);
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
            _standaloneMesh,
            color,
            size,
            ref matrix,
            environmentData
        );
    }

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>("Models/Cairn");
        var cairnNode = model.FindNode("Cairn")!;
        var woodNode = model.FindNode("Wood")!;
        var nodeAbsoluteTransform = BlockMesh.GetNodeAbsoluteTransform(cairnNode);
        var white = Color.White;
        var blockMesh = new BlockMesh();
        blockMesh.AppendModelPrimitive(
            cairnNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform *
            Matrix.CreateRotationX(-(float)Math.PI / 2f) *
            Matrix.CreateTranslation(0.5f, 0f, 0.5f),
            false,
            false,
            false,
            false,
            white
        );
        var blockMesh2 = new BlockMesh();
        blockMesh2.AppendModelPrimitive(
            woodNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform *
            Matrix.CreateRotationX(-(float)Math.PI / 2f) *
            Matrix.CreateTranslation(0.5f, 0f, 0.5f),
            false,
            false,
            false,
            false,
            white
        );
        _blockMesh.AppendBlockMesh(blockMesh);
        _blockMesh.AppendBlockMesh(blockMesh2);
        _standaloneMesh.AppendModelPrimitive(
            cairnNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform *
            Matrix.CreateScale(1.3f) *
            Matrix.CreateRotationX(-(float)Math.PI / 2f) *
            Matrix.CreateTranslation(0f, 0f, 0f),
            false,
            false,
            true,
            false,
            white
        );
        _standaloneMesh.AppendModelPrimitive(
            woodNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform *
            Matrix.CreateScale(1.3f) *
            Matrix.CreateRotationX(-(float)Math.PI / 2f) *
            Matrix.CreateTranslation(0f, 0f, 0f),
            false,
            false,
            true,
            false,
            white
        );
        base.Initialize();
    }
}
