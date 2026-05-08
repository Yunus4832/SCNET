namespace Game.Blocks;

public class AdjustableDelayGateBlock() : RotatableMountedElectricElementBlock(
    "Models/Gates",
    "AdjustableDelayGate",
    0.375f
)
{
    public const int Index = 224;

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

        var delay = GetDelay(Terrain.ExtractData(oldValue));
        var data = SetDelay(0, delay);
        dropValues.Add(new BlockDropValue
        {
            Value = Terrain.MakeBlockValue(224, 0, data),
            Count = 1
        });
    }

    public override ElectricElement CreateElectricElement(
        SubsystemElectricity subsystemElectricity,
        int value,
        int x,
        int y,
        int z
    )
    {
        return new AdjustableDelayGateElectricElement(subsystemElectricity, new CellFace(x, y, z, GetFace(value)));
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
        var data = Terrain.ExtractData(value);
        if (GetFace(value) != face)
        {
            return null;
        }

        var connectorDirection =
            SubsystemElectricity.GetConnectorDirection(GetFace(value), GetRotation(data), connectorFace);
        if (connectorDirection == ElectricConnectorDirection.Bottom)
        {
            return ElectricConnectorType.Input;
        }

        if (connectorDirection is ElectricConnectorDirection.Top or ElectricConnectorDirection.In)
        {
            return ElectricConnectorType.Output;
        }

        return null;
    }

    public static int GetDelay(int data)
    {
        return (data >> 5) & 0xFF;
    }

    public static int SetDelay(int data, int delay)
    {
        return (data & -8161) | ((delay & 0xFF) << 5);
    }
}
