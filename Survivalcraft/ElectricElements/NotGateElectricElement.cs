namespace Game.ElectricElements;

public class NotGateElectricElement(
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
        var num = 0;
        foreach (var connection in Connections)
        {
            if (connection.ConnectorType != ElectricConnectorType.Output && connection.NeighborConnectorType != 0)
            {
                num = (int)MathUtils.Round(
                    connection.NeighborElectricElement.GetOutputVoltage(connection.NeighborConnectorFace) * 15f);
                break;
            }
        }

        _voltage = (~num & 0xF) / 15f;
        return _voltage.UncloseTo(voltage);
    }
}
