using Engine.Graphics;

namespace Game.Blocks;

public class SwitchBlock : MountedElectricElementBlock
{
    public const int Index = 141;

    public BlockMesh[] BlockMeshesByIndex = new BlockMesh[12];

    public BoundingBox[][] CollisionBoxesByIndex = new BoundingBox[12][];

    public BlockMesh StandaloneBlockMesh = new();

    public override void Initialize()
    {
        var model = ContentManager.Get<Model>("Models/Switch");
        var bodyMesh = model.FindMesh("Body")!;
        var boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(
            bodyMesh.ParentBone ??
            throw new InvalidOperationException("Required BodyMesh.ParentBone is null")
        );
        var leverMesh = model.FindMesh("Lever")!;
        var boneAbsoluteTransform2 = BlockMesh.GetBoneAbsoluteTransform(
            leverMesh.ParentBone ??
            throw new InvalidOperationException("Required LeverMesh.ParentBone is null")
        );
        for (var i = 0; i < 6; i++)
        for (var j = 0; j < 2; j++)
        {
            var num = (i << 1) | j;
            var matrix = i >= 4
                ? i != 4
                    ? Matrix.CreateRotationX((float)Math.PI) * Matrix.CreateTranslation(0.5f, 1f, 0.5f)
                    : Matrix.CreateTranslation(0.5f, 0f, 0.5f)
                : Matrix.CreateRotationX((float)Math.PI / 2f) * Matrix.CreateTranslation(0f, 0f, -0.5f) *
                  Matrix.CreateRotationY(i * (float)Math.PI / 2f) * Matrix.CreateTranslation(0.5f, 0.5f, 0.5f);
            var matrix2 = Matrix.CreateRotationX(j == 0 ? MathUtils.DegToRad(30f) : MathUtils.DegToRad(-30f));
            BlockMeshesByIndex[num] = new BlockMesh();
            BlockMeshesByIndex[num].AppendModelMeshPart(bodyMesh.MeshParts[0],
                boneAbsoluteTransform * matrix, false, false, false, false, Color.White);
            BlockMeshesByIndex[num].AppendModelMeshPart(leverMesh.MeshParts[0],
                boneAbsoluteTransform2 * matrix2 * matrix, false, false, false, false, Color.White);
            CollisionBoxesByIndex[num] = [BlockMeshesByIndex[num].CalculateBoundingBox()];
        }

        var matrix3 = Matrix.CreateRotationY(-(float)Math.PI / 2f) * Matrix.CreateRotationZ((float)Math.PI / 2f);
        StandaloneBlockMesh.AppendModelMeshPart(bodyMesh.MeshParts[0], boneAbsoluteTransform * matrix3,
            false, false, false, false, Color.White);
        StandaloneBlockMesh.AppendModelMeshPart(leverMesh.MeshParts[0],
            boneAbsoluteTransform2 * matrix3, false, false, false, false, Color.White);
    }

    public static bool GetLeverState(int value)
    {
        return (Terrain.ExtractData(value) & 1) != 0;
    }

    public static int SetLeverState(int value, bool state)
    {
        return Terrain.ReplaceData(value, state ? Terrain.ExtractData(value) | 1 : Terrain.ExtractData(value) & -2);
    }

    public static int GetVoltageLevel(int data)
    {
        return 15 - ((data >> 4) & 0xF);
    }

    public static int SetVoltageLevel(int data, int voltageLevel)
    {
        return (data & -241) | ((15 - (voltageLevel & 0xF)) << 4);
    }

    public override int GetFace(int value)
    {
        return (Terrain.ExtractData(value) >> 1) & 7;
    }

    public override BlockPlacementData GetPlacementValue(
        SubsystemTerrain subsystemTerrain,
        ComponentMiner componentMiner,
        int value,
        TerrainRaycastResult raycastResult
    )
    {
        var result = default(BlockPlacementData);
        result.Value = Terrain.ReplaceData(value, raycastResult.CellFace.Face << 1);
        var data = SetVoltageLevel(Terrain.ExtractData(result.Value), GetVoltageLevel(Terrain.ExtractData(value)));
        result.Value = Terrain.ReplaceData(value, data);
        result.CellFace = raycastResult.CellFace;
        return result;
    }

    public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
    {
        var num = CalculateIndex(value);
        return num >= CollisionBoxesByIndex.Length ? [] : CollisionBoxesByIndex[num];
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
        var num = CalculateIndex(value);
        if (num >= BlockMeshesByIndex.Length)
        {
            return;
        }

        generator.GenerateMeshVertices(this, x, y, z, BlockMeshesByIndex[num], Color.White, null,
            geometry.SubsetOpaque);
        generator.GenerateWireVertices(value, x, y, z, GetFace(value), 0.25f, Vector2.Zero, geometry.SubsetOpaque);
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

    public override ElectricElement CreateElectricElement(
        SubsystemElectricity subsystemElectricity,
        int value,
        int x,
        int y,
        int z
    )
    {
        return new SwitchElectricElement(subsystemElectricity, new CellFace(x, y, z, GetFace(value)), value);
    }

    public override ElectricConnectorType? GetConnectorType(
        SubsystemTerrain terrain,
        int value,
        int face,
        int connectorFace,
        int x,
        int y,
        int z
    )
    {
        var face2 = GetFace(value);
        if (face == face2 && SubsystemElectricity.GetConnectorDirection(face2, 0, connectorFace).HasValue)
        {
            return ElectricConnectorType.Output;
        }

        return null;
    }

    public int CalculateIndex(int value)
    {
        var face = GetFace(value);
        var leverState = GetLeverState(value);
        return (face << 1) | (leverState ? 1 : 0);
    }
}
