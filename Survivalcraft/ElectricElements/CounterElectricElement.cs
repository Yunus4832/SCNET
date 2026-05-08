namespace Game.ElectricElements;

public class CounterElectricElement : RotateableElectricElement
{
    private int _counter;

    private bool _minusAllowed = true;

    private bool _overflow;

    private bool _plusAllowed = true;

    private bool _resetAllowed = true;

    public CounterElectricElement(SubsystemElectricity subsystemElectricity, CellFace cellFace)
        : base(subsystemElectricity, cellFace)
    {
        var num = subsystemElectricity.ReadPersistentVoltage(cellFace.Point);
        if (!num.HasValue)
        {
            return;
        }

        _counter = (int)MathUtils.Round(MathUtils.Abs(num.Value) * 15f);
        _overflow = num.Value < 0f;
    }

    public override float GetOutputVoltage(int face)
    {
        var connectorDirection = SubsystemElectricity.GetConnectorDirection(CellFaces[0].Face, Rotation, face);
        return connectorDirection switch
        {
            null => 0f,
            ElectricConnectorDirection.Top => _counter / 15f,
            ElectricConnectorDirection.Bottom => _overflow ? 1 : 0,
            _ => 0f
        };
    }

    public override bool Simulate()
    {
        var counter = _counter;
        var overflow = _overflow;
        var flag = false;
        var flag2 = false;
        var flag3 = false;
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
                    flag = IsSignalHigh(
                        connection.NeighborElectricElement.GetOutputVoltage(connection.NeighborConnectorFace));
                }
                else if (connectorDirection == ElectricConnectorDirection.Left)
                {
                    flag2 = IsSignalHigh(
                        connection.NeighborElectricElement.GetOutputVoltage(connection.NeighborConnectorFace));
                }
                else if (connectorDirection == ElectricConnectorDirection.In)
                {
                    flag3 = IsSignalHigh(
                        connection.NeighborElectricElement.GetOutputVoltage(connection.NeighborConnectorFace));
                }
            }
        }

        if (flag && _plusAllowed)
        {
            _plusAllowed = false;
            if (_counter < 15)
            {
                _counter++;
                _overflow = false;
            }
            else
            {
                _counter = 0;
                _overflow = true;
            }
        }
        else if (flag2 && _minusAllowed)
        {
            _minusAllowed = false;
            if (_counter > 0)
            {
                _counter--;
                _overflow = false;
            }
            else
            {
                _counter = 15;
                _overflow = true;
            }
        }
        else if (flag3 && _resetAllowed)
        {
            _counter = 0;
            _overflow = false;
        }

        if (!flag)
        {
            _plusAllowed = true;
        }

        if (!flag2)
        {
            _minusAllowed = true;
        }

        if (!flag3)
        {
            _resetAllowed = true;
        }

        if (_counter == counter && _overflow == overflow)
        {
            return false;
        }

        SubsystemElectricity.WritePersistentVoltage(CellFaces[0].Point, _counter / 15f * (!_overflow ? 1 : -1));
        return true;
    }
}
