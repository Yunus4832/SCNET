using Engine.Graphics;

using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class FourLedBlockPresentation(FourLedBlock block, BlockPresentationCatalog catalog)
    : MountedElectricElementBlockPresentation(block, catalog)
{
    protected new FourLedBlock Block => (FourLedBlock)base.Block;

    public BlockMesh[] BlockMeshesByFace = new BlockMesh[6];

    public BlockMesh StandaloneBlockMesh = new();

    public override string GetDisplayName(SubsystemTerrain? subsystemTerrain, int value)
    {
        var data = Terrain.ExtractData(value);
        var color = FourLedBlock.GetColor(data);
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
        var mountingFace = FourLedBlock.GetMountingFace(Terrain.ExtractData(value));
        if (mountingFace < BlockMeshesByFace.Length)
        {
            generator.GenerateMeshVertices(
                this,
                x,
                y,
                z,
                BlockMeshesByFace[mountingFace],
                Color.White,
                null,
                geometry.SubsetOpaque
            );
            generator.GenerateWireVertices(value, x, y, z, mountingFace, 1f, Vector2.Zero, geometry.SubsetOpaque);
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
        var fourLedNode = ContentManager.Get<Model>("Models/Leds").FindNode("FourLed")!;
        var nodeAbsoluteTransform = BlockMesh.GetNodeAbsoluteTransform(fourLedNode);
        for (var i = 0; i < 6; i++)
        {
            var m = i >= 4
                ? i != 4
                    ? Matrix.CreateRotationX((float)Math.PI) * Matrix.CreateTranslation(0.5f, 1f, 0.5f)
                    : Matrix.CreateTranslation(0.5f, 0f, 0.5f)
                : Matrix.CreateRotationX((float)Math.PI / 2f) *
                  Matrix.CreateTranslation(0f, 0f, -0.5f) *
                  Matrix.CreateRotationY(i * (float)Math.PI / 2f) *
                  Matrix.CreateTranslation(0.5f, 0.5f, 0.5f);
            BlockMeshesByFace[i] = new BlockMesh();
            BlockMeshesByFace[i].AppendModelPrimitive(
                fourLedNode.RequireMesh().Primitives[0],
                nodeAbsoluteTransform * m,
                false,
                false,
                false,
                false,
                Color.White
            );
        }

        var m2 = Matrix.CreateRotationY(-(float)Math.PI / 2f) * Matrix.CreateRotationZ((float)Math.PI / 2f);
        StandaloneBlockMesh = new BlockMesh();
        StandaloneBlockMesh.AppendModelPrimitive(
            fourLedNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform * m2,
            false,
            false,
            false,
            false,
            Color.White
        );
    }
}
