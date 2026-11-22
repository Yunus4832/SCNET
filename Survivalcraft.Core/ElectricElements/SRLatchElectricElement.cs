namespace Game.ElectricElements;

public class SRLatchElectricElement : RotatableElectricElement
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
        var storedVoltage = subsystemElectricity.ReadPersistentVoltage(cellFace.Point);
        if (storedVoltage.HasValue)
        {
            _voltage = storedVoltage.Value;
        }
    }

    public override float GetOutputVoltage(int face)
    {
        return _voltage;
    }

    public override bool Simulate()
    {
        if (!WorldRuntimeContext.For(SubsystemElectricity.Project).IsAuthority)
        {
            return RestorePersistentVoltage(SubsystemElectricity.ReadPersistentVoltage(CellFaces[0].Point) ?? _voltage);
        }

        var voltage = _voltage;
        var setHigh = false;
        var resetHigh = false;
        var clockHigh = false;
        var hasClockInput = false;
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
                    resetHigh = IsSignalHigh(
                        connection.NeighborElectricElement.GetOutputVoltage(connection.NeighborConnectorFace)
                    );
                }
                else if (connectorDirection == ElectricConnectorDirection.Left)
                {
                    setHigh = IsSignalHigh(
                        connection.NeighborElectricElement.GetOutputVoltage(connection.NeighborConnectorFace)
                    );
                }
                else if (connectorDirection == ElectricConnectorDirection.Bottom)
                {
                    clockHigh = IsSignalHigh(
                        connection.NeighborElectricElement.GetOutputVoltage(connection.NeighborConnectorFace)
                    );
                    hasClockInput = true;
                }
            }
        }

        if (hasClockInput)
        {
            if (clockHigh && _clockAllowed)
            {
                _clockAllowed = false;
                if (setHigh && resetHigh)
                {
                    _voltage = !IsSignalHigh(_voltage) ? 1 : 0;
                }
                else if (setHigh)
                {
                    _voltage = 1f;
                }
                else if (resetHigh)
                {
                    _voltage = 0f;
                }
            }
        }
        else if (setHigh && _setAllowed)
        {
            _setAllowed = false;
            _voltage = 1f;
        }
        else if (resetHigh && _resetAllowed)
        {
            _resetAllowed = false;
            _voltage = 0f;
        }

        if (!clockHigh)
        {
            _clockAllowed = true;
        }

        if (!setHigh)
        {
            _setAllowed = true;
        }

        if (!resetHigh)
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

    public override bool RestorePersistentVoltage(float voltage)
    {
        var changed = !_voltage.Equals(voltage);
        _voltage = voltage;
        return changed;
    }
}
