using Engine.Graphics;

using Game.Modding.Blocks;

namespace Game.Blocks.Presentation;

public class HygrometerBlockPresentation(HygrometerBlock block, BlockPresentationCatalog catalog)
    : BlockPresentation(block, catalog)
{
    protected new HygrometerBlock Block => (HygrometerBlock)base.Block;

    public BlockMesh CaseMesh = new();

    public BlockMesh PointerMesh = new();

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
        if (num < Block.MatricesByData.Length)
        {
            var humidity = generator.Terrain.GetHumidity(x, z);
            var radians = MathUtils.Lerp(1.5f, -1.5f, humidity / 15f);
            var matrix = Block.MatricesByData[num];
            var value2 = Block.InvPointerMatrix * Matrix.CreateRotationX(radians) * Block.PointerMatrix * matrix;
            generator.GenerateMeshVertices(this, x, y, z, CaseMesh, Color.White, matrix, geometry.SubsetOpaque);
            generator.GenerateMeshVertices(this, x, y, z, PointerMesh, Color.White, value2, geometry.SubsetOpaque);
            generator.GenerateWireVertices(value, x, y, z, num & 3, 0.25f, Vector2.Zero, geometry.SubsetOpaque);
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
        var num = 8f;
        if (environmentData is { SubsystemTerrain: not null })
        {
            var translation = environmentData.InWorldMatrix.Translation;
            var num2 = Terrain.ToCell(translation.X);
            var num3 = Terrain.ToCell(translation.Z);
            var f = translation.X - num2;
            var f2 = translation.Z - num3;
            float x = environmentData.SubsystemTerrain.Terrain.GetSeasonalHumidity(num2, num3);
            float x2 = environmentData.SubsystemTerrain.Terrain.GetSeasonalHumidity(num2, num3 + 1);
            float x3 = environmentData.SubsystemTerrain.Terrain.GetSeasonalHumidity(num2 + 1, num3);
            float x4 = environmentData.SubsystemTerrain.Terrain.GetSeasonalHumidity(num2 + 1, num3 + 1);
            var x5 = MathUtils.Lerp(x, x2, f2);
            var x6 = MathUtils.Lerp(x3, x4, f2);
            num = MathUtils.Lerp(x5, x6, f);
        }

        var radians = MathUtils.Lerp(1.5f, -1.5f, num / 15f);
        var matrix2 = Matrix.CreateScale(7f * size) * Matrix.CreateTranslation(0f, -0.1f, 0f) * matrix;
        var matrix3 = Block.InvPointerMatrix * Matrix.CreateRotationX(radians) * Block.PointerMatrix * matrix2;
        BlocksManager.DrawMeshBlock(primitivesRenderer, CaseMesh, color, 1f, ref matrix2, environmentData);
        BlocksManager.DrawMeshBlock(primitivesRenderer, PointerMesh, color, 1f, ref matrix3, environmentData);
    }

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>("Models/Hygrometer");
        var caseNode = model.FindNode("Case")!;
        var nodeAbsoluteTransform = BlockMesh.GetNodeAbsoluteTransform(caseNode);
        var pointerNode = model.FindNode("Pointer")!;
        var matrix = Block.PointerMatrix = BlockMesh.GetNodeAbsoluteTransform(pointerNode);
        CaseMesh.AppendModelPrimitive(
            caseNode.RequireMesh().Primitives[0],
            nodeAbsoluteTransform,
            false,
            false,
            true,
            false,
            Color.White
        );
        PointerMesh.AppendModelPrimitive(
            pointerNode.RequireMesh().Primitives[0],
            matrix,
            false,
            false,
            false,
            false,
            Color.White
        );
        base.Initialize();
    }
}
