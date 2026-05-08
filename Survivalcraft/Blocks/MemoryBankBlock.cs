namespace Game.Blocks;

public class MemoryBankBlock() : RotatableMountedElectricElementBlock("Models/Gates", "MemoryBank", 0.875f)
{
    public const int Index = 186;

    public override ElectricElement CreateElectricElement(
        SubsystemElectricity subsystemElectricity,
        int value,
        int x,
        int y,
        int z
    )
    {
        return new MemoryBankElectricElement(subsystemElectricity, new CellFace(x, y, z, GetFace(value)));
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
        return connectorDirection switch
        {
            ElectricConnectorDirection.Right or ElectricConnectorDirection.Left or ElectricConnectorDirection.Bottom
                or ElectricConnectorDirection.In => ElectricConnectorType.Input,
            ElectricConnectorDirection.Top => ElectricConnectorType.Output,
            _ => null
        };
    }
}
