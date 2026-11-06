using Engine.Graphics;

using Game.Modding.Blocks;

using static Game.Blocks.LedBlock;

namespace Game.Blocks.Presentation;

public class LedBlockPresentation(LedBlock block, BlockPresentationCatalog catalog)
    : MountedElectricElementBlockPresentation(block, catalog)
{
    protected new LedBlock Block => (LedBlock)base.Block;

    public readonly BlockMesh[] BlockMeshesByData = new BlockMesh[64];

    public readonly BlockMesh[] StandaloneBlockMeshesByColor = new BlockMesh[8];

    public override string GetDisplayName(SubsystemTerrain? subsystemTerrain, int value)
    {
        var data = Terrain.ExtractData(value);
        var color = GetColor(data);
        return Localization.ClientLanguage.Catalog.Get("LedBlock", color) +
               Localization.ClientLanguage.Catalog.GetBlock(
                   $"{Block.GetType().Name}:{data.ToString()}",
                   "DisplayName"
               );
    }

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
        var color2 = GetColor(Terrain.ExtractData(value));
        BlocksManager.DrawMeshBlock(
            primitivesRenderer,
            StandaloneBlockMeshesByColor[color2],
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
        for (var i = 0; i < 8; i++)
        {
            var color = LedColors[i];
            color *= 0.5f;
            color.A = byte.MaxValue;
            var m = Matrix.CreateRotationY(-(float)Math.PI / 2f) * Matrix.CreateRotationZ((float)Math.PI / 2f);
            StandaloneBlockMeshesByColor[i] = new BlockMesh();
            StandaloneBlockMeshesByColor[i].AppendModelPrimitive(
                ledNode.RequireMesh().Primitives[0],
                nodeAbsoluteTransform * m,
                false,
                false,
                false,
                false,
                Color.White
            );
            StandaloneBlockMeshesByColor[i].AppendModelPrimitive(
                ledBulbNode.RequireMesh().Primitives[0],
                nodeAbsoluteTransform2 * m,
                false,
                false,
                false,
                false,
                color
            );
            for (var j = 0; j < 6; j++)
            {
                var num = SetMountingFace(SetColor(0, i), j);
                var m2 = j >= 4
                    ? j != 4
                        ? Matrix.CreateRotationX((float)Math.PI) * Matrix.CreateTranslation(0.5f, 1f, 0.5f)
                        : Matrix.CreateTranslation(0.5f, 0f, 0.5f)
                    : Matrix.CreateRotationX((float)Math.PI / 2f) *
                      Matrix.CreateTranslation(0f, 0f, -0.5f) *
                      Matrix.CreateRotationY(j * (float)Math.PI / 2f) *
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
                    color
                );
            }
        }
    }
}
