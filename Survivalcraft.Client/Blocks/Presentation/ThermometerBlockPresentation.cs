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
        var num = Terrain.ExtractData(value);
        if (num >= Block.MatricesByData.Length)
        {
            return;
        }

        var num2 = generator.SubsystemMetersBlockBehavior != null
            ? generator.SubsystemMetersBlockBehavior.GetThermometerReading(x, y, z)
            : 8;
        var y2 = MathUtils.Lerp(1f, 4f, num2 / 15f);
        var matrix = Block.MatricesByData[num];
        var value2 = Matrix.CreateTranslation(0f, 0f - FluidBottomPosition, 0f) *
                     Matrix.CreateScale(1f, y2, 1f) *
                     Matrix.CreateTranslation(0f, FluidBottomPosition, 0f) *
                     matrix;
        generator.GenerateMeshVertices(this, x, y, z, CaseMesh, Color.White, matrix, geometry.SubsetOpaque);
        generator.GenerateMeshVertices(this, x, y, z, FluidMesh, Color.White, value2, geometry.SubsetOpaque);
        generator.GenerateWireVertices(value, x, y, z, num & 3, 0.2f, Vector2.Zero, geometry.SubsetOpaque);
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
        var num = 8f;
        if (environmentData is { SubsystemTerrain: not null })
        {
            var translation = environmentData.InWorldMatrix.Translation;
            var num2 = Terrain.ToCell(translation.X);
            var num3 = Terrain.ToCell(translation.Z);
            var f = translation.X - num2;
            var f2 = translation.Z - num3;
            float x = environmentData.SubsystemTerrain.Terrain.GetSeasonalTemperature(num2, num3);
            float x2 = environmentData.SubsystemTerrain.Terrain.GetSeasonalTemperature(num2, num3 + 1);
            float x3 = environmentData.SubsystemTerrain.Terrain.GetSeasonalTemperature(num2 + 1, num3);
            float x4 = environmentData.SubsystemTerrain.Terrain.GetSeasonalTemperature(num2 + 1, num3 + 1);
            var x5 = MathUtils.Lerp(x, x2, f2);
            var x6 = MathUtils.Lerp(x3, x4, f2);
            num = MathUtils.Lerp(x5, x6, f);
        }

        var y = MathUtils.Lerp(1f, 4f, num / 15f);
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
