namespace Game.ElectricElements;

public class DigitalToAnalogConverterElectricElement(
    SubsystemElectricity subsystemElectricity,
    CellFace cellFace
) : RotateableElectricElement(subsystemElectricity, cellFace)
{
    private float _voltage;

    public override float GetOutputVoltage(int face)
    {
        return _voltage;
    }

    public override bool Simulate()
    {
        var voltage = _voltage;
        _voltage = 0f;
        var rotation = Rotation;
        foreach (var connection in Connections)
        {
            if (connection.ConnectorType != ElectricConnectorType.Output && connection.NeighborConnectorType != 0 &&
                IsSignalHigh(connection.NeighborElectricElement.GetOutputVoltage(connection.NeighborConnectorFace)))
            {
                var connectorDirection =
                    SubsystemElectricity.GetConnectorDirection(CellFaces[0].Face, rotation, connection.ConnectorFace);
                if (!connectorDirection.HasValue)
                {
                    continue;
                }

                if (connectorDirection.Value == ElectricConnectorDirection.Top)
                {
                    _voltage += 71f / (339f * (float)Math.PI);
                }

                if (connectorDirection.Value == ElectricConnectorDirection.Right)
                {
                    _voltage += 142f / (339f * (float)Math.PI);
                }

                if (connectorDirection.Value == ElectricConnectorDirection.Bottom)
                {
                    _voltage += 4f / 15f;
                }

                if (connectorDirection.Value == ElectricConnectorDirection.Left)
                {
                    _voltage += 8f / 15f;
                }
            }
        }

        return _voltage.UncloseTo(voltage);
    }
}
