using Engine.Graphics;

using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class ButtonBlockPresentation(ButtonBlock block, BlockPresentationCatalog catalog)
    : MountedElectricElementBlockPresentation(block, catalog)
{
    protected new ButtonBlock Block => (ButtonBlock)base.Block;

    public BlockMesh[] BlockMeshesByFace = new BlockMesh[6];

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
        var face = Block.GetFace(value);
        if (face >= BlockMeshesByFace.Length)
        {
            return;
        }

        generator.GenerateMeshVertices(
            this,
            x,
            y,
            z,
            BlockMeshesByFace[face],
            Color.White,
            null,
            geometry.SubsetOpaque
        );
        generator.GenerateWireVertices(
            value,
            x,
            y,
            z,
            Block.GetFace(value),
            0.25f,
            Vector2.Zero,
            geometry.SubsetOpaque
        );
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
        var model = ContentManager.Get<Model>("Models/Button");
        var buttonNode = model.FindNode("Button")!;
        var nodeAbsoluteTransform = BlockMesh.GetNodeAbsoluteTransform(buttonNode);
        for (var i = 0; i < 6; i++)
        {
            var matrix = i >= 4
                ? i != 4
                    ? Matrix.CreateRotationX((float)Math.PI) * Matrix.CreateTranslation(0.5f, 1f, 0.5f)
                    : Matrix.CreateTranslation(0.5f, 0f, 0.5f)
                : Matrix.CreateRotationX((float)Math.PI / 2f) *
                  Matrix.CreateTranslation(0f, 0f, -0.5f) *
                  Matrix.CreateRotationY(i * (float)Math.PI / 2f) *
                  Matrix.CreateTranslation(0.5f, 0.5f, 0.5f);
            BlockMeshesByFace[i] = new BlockMesh();
            BlockMeshesByFace[i].AppendModelPrimitive(
                buttonNode.RequireMesh().Primitives[0],
                nodeAbsoluteTransform * matrix,
                false,
                false,
                false,
                false,
                Color.White
            );
        }

        var matrix2 = Matrix.CreateRotationY(-(float)Math.PI / 2f) * Matrix.CreateRotationZ((float)Math.PI / 2f);
        StandaloneBlockMesh.AppendModelPrimitive(
            buttonNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform * matrix2,
            false,
            false,
            false,
            false,
            Color.White
        );
    }
}
