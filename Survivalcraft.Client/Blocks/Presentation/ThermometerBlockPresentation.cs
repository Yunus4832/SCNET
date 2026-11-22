using Engine.Graphics;

using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class ThermometerBlockPresentation(ThermometerBlock block, BlockPresentationCatalog catalog)
    : BlockPresentation(block, catalog)
{
    protected new ThermometerBlock Block => (ThermometerBlock)base.Block;

    public BlockMesh CaseMesh = new();

    public BlockMesh FluidMesh = new();

    public override void GenerateTerrainVertices(
        BlockGeometryGenerator generator,
        TerrainGeometry geometry,
        int value,
        int x,
        int y,
        int z
    )
    {
        var mountingData = Terrain.ExtractData(value);
        if (mountingData >= Block.MatricesByData.Length)
        {
            return;
        }

        var reading = generator.SubsystemMetersBlockBehavior != null
            ? generator.SubsystemMetersBlockBehavior.GetThermometerReading(x, y, z)
            : 8;
        var y2 = MathUtils.Lerp(1f, 4f, reading / 15f);
        var matrix = Block.MatricesByData[mountingData];
        var value2 = Matrix.CreateTranslation(0f, 0f - FluidBottomPosition, 0f) *
                     Matrix.CreateScale(1f, y2, 1f) *
                     Matrix.CreateTranslation(0f, FluidBottomPosition, 0f) *
                     matrix;
        generator.GenerateMeshVertices(this, x, y, z, CaseMesh, Color.White, matrix, geometry.SubsetOpaque);
        generator.GenerateMeshVertices(this, x, y, z, FluidMesh, Color.White, value2, geometry.SubsetOpaque);
        generator.GenerateWireVertices(value, x, y, z, mountingData & 3, 0.2f, Vector2.Zero, geometry.SubsetOpaque);
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
        var reading = 8f;
        if (environmentData is { SubsystemTerrain: not null })
        {
            var translation = environmentData.InWorldMatrix.Translation;
            var cellX = Terrain.ToCell(translation.X);
            var cellZ = Terrain.ToCell(translation.Z);
            var f = translation.X - cellX;
            var f2 = translation.Z - cellZ;
            float x = environmentData.SubsystemTerrain.Terrain.GetSeasonalTemperature(cellX, cellZ);
            float x2 = environmentData.SubsystemTerrain.Terrain.GetSeasonalTemperature(cellX, cellZ + 1);
            float x3 = environmentData.SubsystemTerrain.Terrain.GetSeasonalTemperature(cellX + 1, cellZ);
            float x4 = environmentData.SubsystemTerrain.Terrain.GetSeasonalTemperature(cellX + 1, cellZ + 1);
            var x5 = MathUtils.Lerp(x, x2, f2);
            var x6 = MathUtils.Lerp(x3, x4, f2);
            reading = MathUtils.Lerp(x5, x6, f);
        }

        var y = MathUtils.Lerp(1f, 4f, reading / 15f);
        var matrix2 = Matrix.CreateScale(3f * size) * Matrix.CreateTranslation(0f, -0.15f, 0f) * matrix;
        var matrix3 = Matrix.CreateTranslation(0f, 0f - FluidBottomPosition, 0f) *
                      Matrix.CreateScale(1f, y, 1f) *
                      Matrix.CreateTranslation(0f, FluidBottomPosition, 0f) *
                      matrix2;
        BlocksManager.DrawMeshBlock(primitivesRenderer, CaseMesh, color, 1f, ref matrix2, environmentData);
        BlocksManager.DrawMeshBlock(primitivesRenderer, FluidMesh, color, 1f, ref matrix3, environmentData);
    }

    public float FluidBottomPosition;


    public override void Initialize()
    {
        var model = ContentManager.Get<Model>("Models/Thermometer");
        var caseNode = model.FindNode("Case")!;
        var nodeAbsoluteTransform = BlockMesh.GetNodeAbsoluteTransform(caseNode);
        var fluidNode = model.FindNode("Fluid")!;
        var nodeAbsoluteTransform2 = BlockMesh.GetNodeAbsoluteTransform(fluidNode);
        CaseMesh.AppendModelPrimitive(
            caseNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform,
            false,
            false,
            true,
            false,
            Color.White
        );
        FluidMesh.AppendModelPrimitive(
            fluidNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform2,
            false,
            false,
            false,
            false,
            Color.White
        );
        FluidBottomPosition = FluidMesh.Vertices.Min(v => v.Position.Y);
        base.Initialize();
    }
}
