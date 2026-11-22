namespace Game.ElectricElements;

public class XorGateElectricElement(
    SubsystemElectricity subsystemElectricity,
    CellFace cellFace
) : RotatableElectricElement(subsystemElectricity, cellFace)
{
    private float _voltage;

    public override float GetOutputVoltage(int face)
    {
        return _voltage;
    }

    public override bool Simulate()
    {
        var voltage = _voltage;
        int? combinedBits = null;
        foreach (var connection in Connections)
        {
            if (connection.ConnectorType != ElectricConnectorType.Output && connection.NeighborConnectorType != 0)
            {
                var inputBits = (int)MathUtils.Round(
                    connection.NeighborElectricElement.GetOutputVoltage(connection.NeighborConnectorFace) * 15f
                );
                combinedBits = !combinedBits.HasValue ? inputBits : combinedBits ^ inputBits;
            }
        }

        _voltage = combinedBits.HasValue ? combinedBits.Value / 15f : 0f;
        return _voltage.UncloseTo(voltage);
    }
}
