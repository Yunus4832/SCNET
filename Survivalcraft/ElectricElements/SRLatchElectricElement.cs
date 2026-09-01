namespace Game.ElectricElements;

public class SRLatchElectricElement : RotateableElectricElement
{
    private bool _clockAllowed = true;

    private bool _resetAllowed = true;
    private bool _setAllowed = true;

    private float _voltage;

    public SRLatchElectricElement(
        SubsystemElectricity subsystemElectricity,
        CellFace cellFace
    ) : base(subsystemElectricity, cellFace)
    {
        var num = subsystemElectricity.ReadPersistentVoltage(cellFace.Point);
        if (num.HasValue)
        {
            _voltage = num.Value;
        }
    }

    public override float GetOutputVoltage(int face)
    {
        return _voltage;
    }

    public override bool Simulate()
    {
        var voltage = _voltage;
        var flag = false;
        var flag2 = false;
        var flag3 = false;
        var flag4 = false;
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

                if (connectorDirection == ElectricConnectorDirection.Right)
                {
                    flag2 = IsSignalHigh(
                        connection.NeighborElectricElement.GetOutputVoltage(connection.NeighborConnectorFace));
                }
                else if (connectorDirection == ElectricConnectorDirection.Left)
                {
                    flag = IsSignalHigh(
                        connection.NeighborElectricElement.GetOutputVoltage(connection.NeighborConnectorFace));
                }
                else if (connectorDirection == ElectricConnectorDirection.Bottom)
                {
                    flag3 = IsSignalHigh(
                        connection.NeighborElectricElement.GetOutputVoltage(connection.NeighborConnectorFace));
                    flag4 = true;
                }
            }
        }

        if (flag4)
        {
            if (flag3 && _clockAllowed)
            {
                _clockAllowed = false;
                if (flag && flag2)
                {
                    _voltage = !IsSignalHigh(_voltage) ? 1 : 0;
                }
                else if (flag)
                {
                    _voltage = 1f;
                }
                else if (flag2)
                {
                    _voltage = 0f;
                }
            }
        }
        else if (flag && _setAllowed)
        {
            _setAllowed = false;
            _voltage = 1f;
        }
        else if (flag2 && _resetAllowed)
        {
            _resetAllowed = false;
            _voltage = 0f;
        }

        if (!flag3)
        {
            _clockAllowed = true;
        }

        if (!flag)
        {
            _setAllowed = true;
        }

        if (!flag2)
        {
            _resetAllowed = true;
        }

        if (_voltage.CloseTo(voltage))
        {
            return false;
        }

        SubsystemElectricity.WritePersistentVoltage(CellFaces[0].Point, _voltage);
        return true;
    }
}
