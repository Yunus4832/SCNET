namespace Game.ElectricElements;

public class AndGateElectricElement(
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
        var num2 = 15;
        foreach (var connection in Connections)
        {
            if (connection.ConnectorType != ElectricConnectorType.Output && connection.NeighborConnectorType != 0)
            {
                num2 &= (int)MathUtils.Round(
                    connection.NeighborElectricElement.GetOutputVoltage(connection.NeighborConnectorFace) * 15f);
                num++;
            }
        }

        _voltage = num == 2 ? num2 / 15f : 0f;
        return _voltage.UncloseTo(voltage);
    }
}
