namespace Game.ElectricElements;

public abstract class BaseDelayGateElectricElement(
    SubsystemElectricity subsystemElectricity,
    CellFace cellFace
) : RotateableElectricElement(subsystemElectricity, cellFace)
{
    private float _lastStoredVoltage;

    private float _voltage;

    private readonly Dictionary<int, float> _voltagesHistory = new();

    protected abstract int DelaySteps { get; }

    public override float GetOutputVoltage(int face)
    {
        return _voltage;
    }

    public override bool Simulate()
    {
        var voltage = _voltage;
        var delaySteps = DelaySteps;
        var num = 0f;
        foreach (var connection in Connections)
        {
            if (connection.ConnectorType != ElectricConnectorType.Output && connection.NeighborConnectorType != 0)
            {
                num = connection.NeighborElectricElement.GetOutputVoltage(connection.NeighborConnectorFace);
                break;
            }
        }

        if (delaySteps > 0)
        {
            if (_voltagesHistory.TryGetValue(SubsystemElectricity.CircuitStep, out var value))
            {
                _voltage = value;
                _voltagesHistory.Remove(SubsystemElectricity.CircuitStep);
            }

            if (num.UncloseTo(_lastStoredVoltage))
            {
                _lastStoredVoltage = num;
                if (_voltagesHistory.Count < 300)
                {
                    _voltagesHistory[SubsystemElectricity.CircuitStep + DelaySteps] = num;
                    SubsystemElectricity.QueueElectricElementForSimulation(this,
                        SubsystemElectricity.CircuitStep + DelaySteps);
                }
            }
        }
        else
        {
            _voltage = num;
        }

        return _voltage.UncloseTo(voltage);
    }
}
