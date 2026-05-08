namespace Game.ElectricElements;

public class AnalogToDigitalConverterElectricElement(
    SubsystemElectricity subsystemElectricity,
    CellFace cellFace
) : RotateableElectricElement(subsystemElectricity, cellFace)
{
    private int _bits;

    public override float GetOutputVoltage(int face)
    {
        var connectorDirection = SubsystemElectricity.GetConnectorDirection(CellFaces[0].Face, Rotation, face);
        return connectorDirection switch
        {
            null => 0f,
            ElectricConnectorDirection.Top => (_bits & 1) != 0 ? 1 : 0,
            ElectricConnectorDirection.Right => (_bits & 2) != 0 ? 1 : 0,
            ElectricConnectorDirection.Bottom => (_bits & 4) != 0 ? 1 : 0,
            ElectricConnectorDirection.Left => (_bits & 8) != 0 ? 1 : 0,
            _ => 0f
        };
    }

    public override bool Simulate()
    {
        var bits = _bits;
        var rotation = Rotation;
        foreach (var connection in Connections)
        {
            if (connection.ConnectorType != ElectricConnectorType.Output && connection.NeighborConnectorType != 0)
            {
                var connectorDirection =
                    SubsystemElectricity.GetConnectorDirection(CellFaces[0].Face, rotation, connection.ConnectorFace);
                if (connectorDirection is not ElectricConnectorDirection.In)
                {
                    continue;
                }

                var outputVoltage =
                    connection.NeighborElectricElement.GetOutputVoltage(connection.NeighborConnectorFace);
                _bits = (int)MathUtils.Round(outputVoltage * 15f);
            }
        }

        return _bits != bits;
    }
}
