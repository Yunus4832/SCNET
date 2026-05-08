namespace Game.Blocks;

public class DigitalToAnalogConverterBlock() : RotatableMountedElectricElementBlock(
    "Models/Gates",
    "DigitalToAnalogConverter",
    0.375f
)
{
    public const int Index = 180;

    public override ElectricElement CreateElectricElement(
        SubsystemElectricity subsystemElectricity,
        int value,
        int x,
        int y,
        int z
    )
    {
        return new DigitalToAnalogConverterElectricElement(subsystemElectricity, new CellFace(x, y, z, GetFace(value)));
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

        var connectorDirection = SubsystemElectricity.GetConnectorDirection(
            GetFace(value),
            GetRotation(data),
            connectorFace
        );
        return connectorDirection switch
        {
            ElectricConnectorDirection.In => ElectricConnectorType.Output,
            ElectricConnectorDirection.Bottom
                or ElectricConnectorDirection.Top
                or ElectricConnectorDirection.Right
                or ElectricConnectorDirection.Left => ElectricConnectorType.Input,
            _ => null
        };
    }
}
