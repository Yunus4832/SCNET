using Engine.Graphics;

using Game.Modding.Blocks;

using static Game.Blocks.MulticoloredLedBlock;

namespace Game.Blocks.Presentation;

public class MulticoloredLedBlockPresentation(MulticoloredLedBlock block, BlockPresentationCatalog catalog)
    : MountedElectricElementBlockPresentation(block, catalog)
{
    protected new MulticoloredLedBlock Block => (MulticoloredLedBlock)base.Block;

    public BlockMesh[] BlockMeshesByData = new BlockMesh[6];

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
        if (num >= BlockMeshesByData.Length)
        {
            return;
        }

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
        var model = ContentManager.Get<Model>("Models/Leds");
        var ledNode = model.FindNode("Led")!;
        var ledBulbNode = model.FindNode("LedBulb")!;
        var nodeAbsoluteTransform = BlockMesh.GetNodeAbsoluteTransform(ledNode);
        var nodeAbsoluteTransform2 = BlockMesh.GetNodeAbsoluteTransform(ledBulbNode);
        var m = Matrix.CreateRotationY(-(float)Math.PI / 2f) * Matrix.CreateRotationZ((float)Math.PI / 2f);
        StandaloneBlockMesh = new BlockMesh();
        StandaloneBlockMesh.AppendModelPrimitive(
            ledNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform * m,
            false,
            false,
            false,
            false,
            Color.White
        );
        StandaloneBlockMesh.AppendModelPrimitive(
            ledBulbNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform2 * m,
            false,
            false,
            false,
            false,
            new Color(48, 48, 48)
        );
        for (var i = 0; i < 6; i++)
        {
            var num = SetMountingFace(0, i);
            var m2 = i >= 4
                ? i != 4
                    ? Matrix.CreateRotationX((float)Math.PI) * Matrix.CreateTranslation(0.5f, 1f, 0.5f)
                    : Matrix.CreateTranslation(0.5f, 0f, 0.5f)
                : Matrix.CreateRotationX((float)Math.PI / 2f) *
                  Matrix.CreateTranslation(0f, 0f, -0.5f) *
                  Matrix.CreateRotationY(i * (float)Math.PI / 2f) *
                  Matrix.CreateTranslation(0.5f, 0.5f, 0.5f);
            BlockMeshesByData[num] = new BlockMesh();
            BlockMeshesByData[num].AppendModelPrimitive(
                ledNode.RequireMesh().Primitives[0],
                nodeAbsoluteTransform * m2,
                false,
                false,
                false,
                false,
                Color.White
            );
            BlockMeshesByData[num].AppendModelPrimitive(
                ledBulbNode.RequireMesh().Primitives[0],
                nodeAbsoluteTransform2 * m2,
                false,
                false,
                false,
                false,
                new Color(48, 48, 48)
            );
        }
    }
}
