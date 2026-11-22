namespace Game.ElectricElements;

public class RandomGeneratorElectricElement : RotatableElectricElement
{
    private static readonly Random _innerRandom = new();

    private bool _clockAllowed = true;

    private float _voltage;

    public RandomGeneratorElectricElement(
        SubsystemElectricity subsystemElectricity,
        CellFace cellFace
    ) : base(subsystemElectricity, cellFace)
    {
        var storedVoltage = SubsystemElectricity.ReadPersistentVoltage(CellFaces[0].Point);
        _voltage = storedVoltage ?? GetRandomVoltage();
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
        var clockTriggered = false;
        var hasInput = false;
        _ = Rotation;
        foreach (var connection in Connections)
        {
            if (connection.ConnectorType != ElectricConnectorType.Output &&
                connection.NeighborConnectorType != ElectricConnectorType.Input)
            {
                if (IsSignalHigh(connection.NeighborElectricElement.GetOutputVoltage(connection.NeighborConnectorFace)))
                {
                    if (_clockAllowed)
                    {
                        clockTriggered = true;
                        _clockAllowed = false;
                    }
                }
                else
                {
                    _clockAllowed = true;
                }

                hasInput = true;
            }
        }

        if (hasInput)
        {
            if (clockTriggered)
            {
                _voltage = GetRandomVoltage();
            }
        }
        else
        {
            _voltage = GetRandomVoltage();
            SubsystemElectricity.QueueElectricElementForSimulation(
                this,
                SubsystemElectricity.CircuitStep + MathUtils.Max((int)(_innerRandom.Float(0.25f, 0.75f) / 0.01f), 1)
            );
        }

        if (_voltage.CloseTo(voltage))
        {
            return false;
        }

        SubsystemElectricity.WritePersistentVoltage(CellFaces[0].Point, _voltage);
        return true;
    }

    public float GetRandomVoltage()
    {
        float v = 0;
        if (WorldRuntimeContext.For(SubsystemElectricity.Project).IsAuthority)
        {
            return _innerRandom.Int(0, 15) / 15f;
        }

        var vv = SubsystemElectricity.ReadPersistentVoltage(CellFaces[0].Point);
        if (vv.HasValue)
        {
            v = vv.Value;
        }

        return v;
    }

    public override bool RestorePersistentVoltage(float voltage)
    {
        var changed = !_voltage.Equals(voltage);
        _voltage = voltage;
        return changed;
    }
}
