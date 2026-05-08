namespace Game.Blocks;

public class OrGateBlock() : RotatableMountedElectricElementBlock("Models/Gates", "OrGate", 0.375f)
{
    public const int Index = 143;

    public override ElectricElement CreateElectricElement(
        SubsystemElectricity subsystemElectricity,
        int value,
        int x,
        int y,
        int z
    )
    {
        return new OrGateElectricElement(subsystemElectricity, new CellFace(x, y, z, GetFace(value)));
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
            ElectricConnectorDirection.Right or ElectricConnectorDirection.Left => ElectricConnectorType.Input,
            ElectricConnectorDirection.Top or ElectricConnectorDirection.In => ElectricConnectorType.Output,
            _ => null
        };
    }
}
