using Engine.Graphics;

using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class TorchBlockPresentation(TorchBlock block, BlockPresentationCatalog catalog)
    : BlockPresentation(block, catalog)
{
    protected new TorchBlock Block => (TorchBlock)base.Block;

    public BlockMesh[] BlockMeshesByVariant = new BlockMesh[5];

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
        if (num < BlockMeshesByVariant.Length)
        {
            generator.GenerateMeshVertices(
                this,
                x,
                y,
                z,
                BlockMeshesByVariant[num],
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
            2f * size,
            ref matrix,
            environmentData
        );
    }

    public override void Initialize()
    {
        for (var i = 0; i < BlockMeshesByVariant.Length; i++)
        {
            BlockMeshesByVariant[i] = new BlockMesh();
        }

        var model = ContentManager.Get<Model>("Models/Torch");
        var torchNode = model.FindNode("Torch")!;
        var nodeAbsoluteTransform = BlockMesh.GetNodeAbsoluteTransform(torchNode);
        var flameNode = model.FindNode("Flame")!;
        var nodeAbsoluteTransform2 = BlockMesh.GetNodeAbsoluteTransform(flameNode);
        var m = Matrix.CreateRotationX(0.6f) *
                Matrix.CreateRotationY(0f) *
                Matrix.CreateTranslation(0.5f, 0.15f, -0.05f);
        BlockMeshesByVariant[0].AppendModelPrimitive(
            torchNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform * m,
            false,
            false,
            false,
            false,
            Color.White
        );
        BlockMeshesByVariant[0].AppendModelPrimitive(
            flameNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform2 * m,
            true,
            false,
            false,
            false,
            Color.White
        );
        m = Matrix.CreateRotationX(0.6f) *
            Matrix.CreateRotationY((float)Math.PI / 2f) *
            Matrix.CreateTranslation(-0.05f, 0.15f, 0.5f);
        BlockMeshesByVariant[1].AppendModelPrimitive(
            torchNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform * m,
            false,
            false,
            false,
            false,
            Color.White
        );
        BlockMeshesByVariant[1].AppendModelPrimitive(
            flameNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform2 * m,
            true,
            false,
            false,
            false,
            Color.White
        );
        m = Matrix.CreateRotationX(0.6f) *
            Matrix.CreateRotationY((float)Math.PI) *
            Matrix.CreateTranslation(0.5f, 0.15f, 1.05f);
        BlockMeshesByVariant[2].AppendModelPrimitive(
            torchNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform * m,
            false,
            false,
            false,
            false,
            Color.White
        );
        BlockMeshesByVariant[2].AppendModelPrimitive(
            flameNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform2 * m,
            true,
            false,
            false,
            false,
            Color.White
        );
        m = Matrix.CreateRotationX(0.6f) *
            Matrix.CreateRotationY(4.712389f) *
            Matrix.CreateTranslation(1.05f, 0.15f, 0.5f);
        BlockMeshesByVariant[3].AppendModelPrimitive(
            torchNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform * m,
            false,
            false,
            false,
            false,
            Color.White
        );
        BlockMeshesByVariant[3].AppendModelPrimitive(
            flameNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform2 * m,
            true,
            false,
            false,
            false,
            Color.White
        );
        m = Matrix.CreateTranslation(0.5f, 0f, 0.5f);
        BlockMeshesByVariant[4].AppendModelPrimitive(
            torchNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform * m,
            false,
            false,
            false,
            false,
            Color.White
        );
        BlockMeshesByVariant[4].AppendModelPrimitive(
            flameNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform2 * m,
            true,
            false,
            false,
            false,
            Color.White
        );
        StandaloneBlockMesh.AppendModelPrimitive(
            torchNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform * Matrix.CreateTranslation(0f, -0.25f, 0f),
            false,
            false,
            false,
            false,
            Color.White
        );
        base.Initialize();
    }
}
