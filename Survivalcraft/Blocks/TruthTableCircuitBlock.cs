namespace Game.Blocks;

public class TruthTableCircuitBlock() : RotatableMountedElectricElementBlock("Models/Gates", "TruthTableCircuit", 0.5f)
{
    public const int Index = 188;

    public override ElectricElement CreateElectricElement(
        SubsystemElectricity subsystemElectricity,
        int value,
        int x,
        int y,
        int z
    )
    {
        return new TruthTableCircuitElectricElement(subsystemElectricity, new CellFace(x, y, z, GetFace(value)));
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
            ElectricConnectorDirection.Right or
                ElectricConnectorDirection.Left or
                ElectricConnectorDirection.Bottom or
                ElectricConnectorDirection.Top => ElectricConnectorType.Input,
            ElectricConnectorDirection.In => ElectricConnectorType.Output,
            _ => null
        };
    }
}
