using Game.Blocks.Collision;

namespace Game.Blocks;

public class BatteryBlock : Block, IElectricElementBlock
{
    public BoundingBox[] CollisionBoxes = new BoundingBox[1];


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

    public override ResourceId? CollisionModelAsset { get; } = ResourceId.Parse("game:Models/Battery.glb");

    public override void Initialize()
    {
        CollisionGeometryBuilder blockPositions = new();
        var model = Catalog.CollisionGeometry.Get(CollisionModelAsset!.Value);
        var batteryNode = model.GetNode("Battery")!;
        var nodeAbsoluteTransform = batteryNode.AbsoluteTransform;
        blockPositions.Append(
            batteryNode.Primitives[0],
            nodeAbsoluteTransform * Matrix.CreateTranslation(0.5f, 0f, 0.5f)
        );
        CollisionBoxes[0] = blockPositions.GetBounds();
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
        dropValues.Add(
            new BlockDropValue
            {
                Value = Terrain.MakeBlockValue(BlockIndex, data),
                Count = 1
            }
        );
    }

    public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
    {
        return CollisionBoxes;
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
