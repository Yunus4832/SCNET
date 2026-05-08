using Engine.Graphics;

namespace Game.Blocks;

public class ThermometerBlock : Block, IElectricElementBlock
{
    public const int Index = 120;

    public BlockMesh CaseMesh = new();

    public BoundingBox[][] CollisionBoxesByData = new BoundingBox[4][];

    public float FluidBottomPosition;

    public BlockMesh FluidMesh = new();

    public Matrix[] MatricesByData = new Matrix[4];

    public ElectricElement CreateElectricElement(
        SubsystemElectricity subsystemElectricity,
        int value,
        int x,
        int y,
        int z
    )
    {
        var num = Terrain.ExtractData(value);
        return new ThermometerElectricElement(subsystemElectricity, new CellFace(x, y, z, num & 3));
    }

    public ElectricConnectorType? GetConnectorType(
        SubsystemTerrain terrain,
        int value,
        int face,
        int connectorFace,
        int x,
        int y,
        int z
    )
    {
        if ((Terrain.ExtractData(value) & 3) == face)
        {
            return ElectricConnectorType.Output;
        }

        return null;
    }

    public int GetConnectionMask(int value)
    {
        return int.MaxValue;
    }

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>("Models/Thermometer");
        var caseMesh = model.FindMesh("Case")!;
        var boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(
            caseMesh.ParentBone ??
            throw new InvalidOperationException("Required CaseMesh.ParentBone is null")
        );
        var fluidMesh = model.FindMesh("Fluid")!;
        var boneAbsoluteTransform2 = BlockMesh.GetBoneAbsoluteTransform(
            fluidMesh.ParentBone ??
            throw new InvalidOperationException("Required FluidMesh.ParentBone is null")
        );
        CaseMesh.AppendModelMeshPart(caseMesh.MeshParts[0], boneAbsoluteTransform, false, false, true,
            false, Color.White);
        FluidMesh.AppendModelMeshPart(fluidMesh.MeshParts[0], boneAbsoluteTransform2, false, false,
            false, false, Color.White);
        for (var i = 0; i < 4; i++)
        {
            MatricesByData[i] = Matrix.CreateScale(1.5f) * Matrix.CreateTranslation(0.95f, 0.15f, 0.5f) *
                                Matrix.CreateTranslation(-0.5f, 0f, -0.5f) *
                                Matrix.CreateRotationY((i + 1) * (float)Math.PI / 2f) *
                                Matrix.CreateTranslation(0.5f, 0f, 0.5f);
            CollisionBoxesByData[i] = [CaseMesh.CalculateBoundingBox(MatricesByData[i])];
        }

        FluidBottomPosition = FluidMesh.Vertices.Min(v => v.Position.Y);
        base.Initialize();
    }

    public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
    {
        var num = Terrain.ExtractData(value);
        return num < CollisionBoxesByData.Length ? CollisionBoxesByData[num] : [];
    }

    public override BlockPlacementData GetPlacementValue(
        SubsystemTerrain subsystemTerrain,
        ComponentMiner componentMiner,
        int value,
        TerrainRaycastResult raycastResult
    )
    {
        var value2 = raycastResult.CellFace.Face switch
        {
            0 => Terrain.ReplaceData(Terrain.ReplaceContents(0, 120), 0),
            1 => Terrain.ReplaceData(Terrain.ReplaceContents(0, 120), 1),
            2 => Terrain.ReplaceData(Terrain.ReplaceContents(0, 120), 2),
            3 => Terrain.ReplaceData(Terrain.ReplaceContents(0, 120), 3),
            _ => 0
        };
        BlockPlacementData result = default;
        result.Value = value2;
        result.CellFace = raycastResult.CellFace;
        return result;
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
        if (num >= MatricesByData.Length)
        {
            return;
        }

        var num2 = generator.SubsystemMetersBlockBehavior != null
            ? generator.SubsystemMetersBlockBehavior.GetThermometerReading(x, y, z)
            : 8;
        var y2 = MathUtils.Lerp(1f, 4f, num2 / 15f);
        var matrix = MatricesByData[num];
        var value2 = Matrix.CreateTranslation(0f, 0f - FluidBottomPosition, 0f) * Matrix.CreateScale(1f, y2, 1f) *
                     Matrix.CreateTranslation(0f, FluidBottomPosition, 0f) * matrix;
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
        var matrix3 = Matrix.CreateTranslation(0f, 0f - FluidBottomPosition, 0f) * Matrix.CreateScale(1f, y, 1f) *
                      Matrix.CreateTranslation(0f, FluidBottomPosition, 0f) * matrix2;
        BlocksManager.DrawMeshBlock(primitivesRenderer, CaseMesh, color, 1f, ref matrix2, environmentData);
        BlocksManager.DrawMeshBlock(primitivesRenderer, FluidMesh, color, 1f, ref matrix3, environmentData);
    }
}
