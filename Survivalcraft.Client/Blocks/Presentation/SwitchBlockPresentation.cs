using Engine.Graphics;

using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class SwitchBlockPresentation(SwitchBlock block, BlockPresentationCatalog catalog)
    : MountedElectricElementBlockPresentation(block, catalog)
{
    protected new SwitchBlock Block => (SwitchBlock)base.Block;

    public BlockMesh[] BlockMeshesByIndex = new BlockMesh[12];

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
        var num = Block.CalculateIndex(value);
        if (num >= BlockMeshesByIndex.Length)
        {
            return;
        }

        generator.GenerateMeshVertices(
            this,
            x,
            y,
            z,
            BlockMeshesByIndex[num],
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
        var model = ContentManager.Get<Model>("Models/Switch");
        var bodyNode = model.FindNode("Body")!;
        var nodeAbsoluteTransform = BlockMesh.GetNodeAbsoluteTransform(bodyNode);
        var leverNode = model.FindNode("Lever")!;
        var nodeAbsoluteTransform2 = BlockMesh.GetNodeAbsoluteTransform(leverNode);
        for (var i = 0; i < 6; i++)
        {
            for (var j = 0; j < 2; j++)
            {
                var num = (i << 1) | j;
                var matrix = i >= 4
                    ? i != 4
                        ? Matrix.CreateRotationX((float)Math.PI) * Matrix.CreateTranslation(0.5f, 1f, 0.5f)
                        : Matrix.CreateTranslation(0.5f, 0f, 0.5f)
                    : Matrix.CreateRotationX((float)Math.PI / 2f) *
                      Matrix.CreateTranslation(0f, 0f, -0.5f) *
                      Matrix.CreateRotationY(i * (float)Math.PI / 2f) *
                      Matrix.CreateTranslation(0.5f, 0.5f, 0.5f);
                var matrix2 = Matrix.CreateRotationX(j == 0 ? MathUtils.DegToRad(30f) : MathUtils.DegToRad(-30f));
                BlockMeshesByIndex[num] = new BlockMesh();
                BlockMeshesByIndex[num].AppendModelPrimitive(
                    bodyNode.RequireMesh().Primitives[0],
                    nodeAbsoluteTransform * matrix,
                    false,
                    false,
                    false,
                    false,
                    Color.White
                );
                BlockMeshesByIndex[num].AppendModelPrimitive(
                    leverNode.RequireMesh().Primitives[0],
                    nodeAbsoluteTransform2 * matrix2 * matrix,
                    false,
                    false,
                    false,
                    false,
                    Color.White
                );
            }
        }

        var matrix3 = Matrix.CreateRotationY(-(float)Math.PI / 2f) * Matrix.CreateRotationZ((float)Math.PI / 2f);
        StandaloneBlockMesh.AppendModelPrimitive(
            bodyNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform * matrix3,
            false,
            false,
            false,
            false,
            Color.White
        );
        StandaloneBlockMesh.AppendModelPrimitive(
            leverNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform2 * matrix3,
            false,
            false,
            false,
            false,
            Color.White
        );
    }
}
