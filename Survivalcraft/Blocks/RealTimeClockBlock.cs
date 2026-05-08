namespace Game.Blocks;

public class RealTimeClockBlock() : RotatableMountedElectricElementBlock("Models/Gates", "RealTimeClock", 0.5f)
{
    public const int Index = 187;

    public override ElectricElement CreateElectricElement(
        SubsystemElectricity subsystemElectricity,
        int value,
        int x,
        int y,
        int z
    )
    {
        return new RealTimeClockElectricElement(subsystemElectricity, new CellFace(x, y, z, GetFace(value)));
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
        if (connectorDirection is
            ElectricConnectorDirection.Top or
            ElectricConnectorDirection.Right or
            ElectricConnectorDirection.Left or
            ElectricConnectorDirection.Bottom or
            ElectricConnectorDirection.In)
        {
            return ElectricConnectorType.Output;
        }

        return null;
    }
}
