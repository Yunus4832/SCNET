namespace Game.ElectricElements;

public class TruthTableCircuitElectricElement(
    SubsystemElectricity subsystemElectricity,
    CellFace cellFace
) : RotateableElectricElement(subsystemElectricity, cellFace)
{
    private readonly SubsystemTruthTableCircuitBlockBehavior _subsystemTruthTableCircuitBlockBehavior =
        subsystemElectricity.Project.FindSubsystem<SubsystemTruthTableCircuitBlockBehavior>(true)!;

    private float _voltage;

    public override float GetOutputVoltage(int face)
    {
        return _voltage;
    }

    public override bool Simulate()
    {
        var voltage = _voltage;
        var num = 0;
        var rotation = Rotation;
        foreach (var connection in Connections)
        {
            if (connection.ConnectorType != ElectricConnectorType.Output && connection.NeighborConnectorType != 0)
            {
                var connectorDirection =
                    SubsystemElectricity.GetConnectorDirection(CellFaces[0].Face, rotation, connection.ConnectorFace);
                if (!connectorDirection.HasValue)
                {
                    continue;
                }

                if (connectorDirection == ElectricConnectorDirection.Top)
                {
                    if (IsSignalHigh(
                            connection.NeighborElectricElement.GetOutputVoltage(connection.NeighborConnectorFace)))
                    {
                        num |= 1;
                    }
                }
                else if (connectorDirection == ElectricConnectorDirection.Right)
                {
                    if (IsSignalHigh(
                            connection.NeighborElectricElement.GetOutputVoltage(connection.NeighborConnectorFace)))
                    {
                        num |= 2;
                    }
                }
                else if (connectorDirection == ElectricConnectorDirection.Bottom)
                {
                    if (IsSignalHigh(
                            connection.NeighborElectricElement.GetOutputVoltage(connection.NeighborConnectorFace)))
                    {
                        num |= 4;
                    }
                }
                else if (connectorDirection == ElectricConnectorDirection.Left && IsSignalHigh(
                             connection.NeighborElectricElement.GetOutputVoltage(connection.NeighborConnectorFace)))
                {
                    num |= 8;
                }
            }
        }

        var blockData = _subsystemTruthTableCircuitBlockBehavior.GetBlockData(CellFaces[0].Point);
        _voltage = blockData != null ? blockData.Data[num] / 15f : 0f;
        return _voltage.UncloseTo(voltage);
    }
}
