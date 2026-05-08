using Engine.Graphics;

namespace Game.Blocks;

public class BatteryBlock : Block, IElectricElementBlock
{
    public const int Index = 138;

    public BlockMesh BlockMesh = new();

    public BoundingBox[] CollisionBoxes = new BoundingBox[1];

    public BlockMesh StandaloneBlockMesh = new();

    public ElectricElement CreateElectricElement(
        SubsystemElectricity subsystemElectricity,
        int value,
        int x,
        int y,
        int z
    )
    {
        return new BatteryElectricElement(subsystemElectricity, new CellFace(x, y, z, 4));
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
        if (face == 4 && SubsystemElectricity.GetConnectorDirection(4, 0, connectorFace).HasValue)
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
        var model = ContentManager.Get<Model>("Models/Battery");
        var batteryMesh = model.FindMesh("Battery")!;
        var boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(
            batteryMesh.ParentBone ??
            throw new InvalidOperationException("Required BatteryMesh.ParentBone is null")
        );
        StandaloneBlockMesh.AppendModelMeshPart(batteryMesh.MeshParts[0],
            boneAbsoluteTransform * Matrix.CreateTranslation(0f, -0.5f, 0f), false, false, false, false, Color.White);
        BlockMesh.AppendModelMeshPart(batteryMesh.MeshParts[0],
            boneAbsoluteTransform * Matrix.CreateTranslation(0.5f, 0f, 0.5f), false, false, false, false, Color.White);
        CollisionBoxes[0] = BlockMesh.CalculateBoundingBox();
    }

    public override void GetDropValues(
        SubsystemTerrain subsystemTerrain,
        int oldValue,
        int newValue,
        int toolLevel,
        List<BlockDropValue> dropValues,
        out bool showDebris
    )
    {
        showDebris = true;
        if (toolLevel < RequiredToolLevel)
        {
            return;
        }

        var data = Terrain.ExtractData(oldValue);
        dropValues.Add(new BlockDropValue
        {
            Value = Terrain.MakeBlockValue(138, 0, data),
            Count = 1
        });
    }

    public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
    {
        return CollisionBoxes;
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
        generator.GenerateMeshVertices(this, x, y, z, BlockMesh, Color.White, null, geometry.SubsetOpaque);
        generator.GenerateWireVertices(value, x, y, z, 4, 0.72f, Vector2.Zero, geometry.SubsetOpaque);
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
            1f * size,
            ref matrix,
            environmentData
        );
    }

    public static int GetVoltageLevel(int data)
    {
        return 15 - (data & 0xF);
    }

    public static int SetVoltageLevel(int data, int voltageLevel)
    {
        return (data & -16) | (15 - (voltageLevel & 0xF));
    }
}
