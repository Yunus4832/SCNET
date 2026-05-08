namespace Game.ElectricElements;

public class XorGateElectricElement(
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
        int? num = null;
        foreach (var connection in Connections)
        {
            if (connection.ConnectorType != ElectricConnectorType.Output && connection.NeighborConnectorType != 0)
            {
                var num2 = (int)MathUtils.Round(
                    connection.NeighborElectricElement.GetOutputVoltage(connection.NeighborConnectorFace) * 15f);
                num = !num.HasValue ? num2 : num ^ num2;
            }
        }

        _voltage = num.HasValue ? num.Value / 15f : 0f;
        return _voltage.UncloseTo(voltage);
    }
}
