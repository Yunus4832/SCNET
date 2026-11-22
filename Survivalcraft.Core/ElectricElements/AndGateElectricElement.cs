namespace Game.ElectricElements;

public class AndGateElectricElement(
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
        var inputCount = 0;
        var combinedBits = 15;
        foreach (var connection in Connections)
        {
            if (connection.ConnectorType != ElectricConnectorType.Output && connection.NeighborConnectorType != 0)
            {
                combinedBits &= (int)MathUtils.Round(
                    connection.NeighborElectricElement.GetOutputVoltage(connection.NeighborConnectorFace) * 15f
                );
                inputCount++;
            }
        }

        _voltage = inputCount == 2 ? combinedBits / 15f : 0f;
        return _voltage.UncloseTo(voltage);
    }
}
