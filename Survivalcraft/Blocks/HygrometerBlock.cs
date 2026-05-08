using Engine.Graphics;

namespace Game.Blocks;

public class HygrometerBlock : Block, IElectricElementBlock
{
    public const int Index = 121;

    public BlockMesh CaseMesh = new();

    public BoundingBox[][] CollisionBoxesByData = new BoundingBox[4][];

    public Matrix InvPointerMatrix;

    public Matrix[] MatricesByData = new Matrix[4];

    public Matrix PointerMatrix;

    public BlockMesh PointerMesh = new();

    public ElectricElement CreateElectricElement(
        SubsystemElectricity subsystemElectricity,
        int value,
        int x,
        int y,
        int z
    )
    {
        var num = Terrain.ExtractData(value);
        return new HygrometerElectricElement(subsystemElectricity, new CellFace(x, y, z, num & 3));
    }

    public ElectricConnectorType? GetConnectorType(SubsystemTerrain terrain, int value, int face, int connectorFace,
        int x, int y, int z)
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
        var model = ContentManager.Get<Model>("Models/Hygrometer");
        var caseMesh = model.FindMesh("Case")!;
        var boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(
            caseMesh.ParentBone ??
            throw new InvalidOperationException("Required CaseMesh.ParentBone is null")
        );
        var pointerMesh = model.FindMesh("Pointer")!;
        var matrix = PointerMatrix = BlockMesh.GetBoneAbsoluteTransform(
            pointerMesh.ParentBone ??
            throw new InvalidOperationException("Required PointerMesh.ParentBone is null")
        );
        InvPointerMatrix = Matrix.Invert(PointerMatrix);
        CaseMesh.AppendModelMeshPart(caseMesh.MeshParts[0], boneAbsoluteTransform, false, false, true,
            false, Color.White);
        PointerMesh.AppendModelMeshPart(pointerMesh.MeshParts[0], matrix, false, false, false, false,
            Color.White);
        for (var i = 0; i < 4; i++)
        {
            MatricesByData[i] = Matrix.CreateScale(5f) * Matrix.CreateTranslation(0.95f, 0.15f, 0.5f) *
                                Matrix.CreateTranslation(-0.5f, 0f, -0.5f) *
                                Matrix.CreateRotationY((i + 1) * (float)Math.PI / 2f) *
                                Matrix.CreateTranslation(0.5f, 0f, 0.5f);
            CollisionBoxesByData[i] = new BoundingBox[1]
            {
                CaseMesh.CalculateBoundingBox(MatricesByData[i])
            };
        }

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
        var value2 = 0;
        if (raycastResult.CellFace.Face == 0)
        {
            value2 = Terrain.ReplaceData(Terrain.ReplaceContents(0, 121), 0);
        }

        if (raycastResult.CellFace.Face == 1)
        {
            value2 = Terrain.ReplaceData(Terrain.ReplaceContents(0, 121), 1);
        }

        if (raycastResult.CellFace.Face == 2)
        {
            value2 = Terrain.ReplaceData(Terrain.ReplaceContents(0, 121), 2);
        }

        if (raycastResult.CellFace.Face == 3)
        {
            value2 = Terrain.ReplaceData(Terrain.ReplaceContents(0, 121), 3);
        }

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
        if (num < MatricesByData.Length)
        {
            var humidity = generator.Terrain.GetHumidity(x, z);
            var radians = MathUtils.Lerp(1.5f, -1.5f, humidity / 15f);
            var matrix = MatricesByData[num];
            var value2 = InvPointerMatrix * Matrix.CreateRotationX(radians) * PointerMatrix * matrix;
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
        var matrix3 = InvPointerMatrix * Matrix.CreateRotationX(radians) * PointerMatrix * matrix2;
        BlocksManager.DrawMeshBlock(primitivesRenderer, CaseMesh, color, 1f, ref matrix2, environmentData);
        BlocksManager.DrawMeshBlock(primitivesRenderer, PointerMesh, color, 1f, ref matrix3, environmentData);
    }
}
