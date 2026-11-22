namespace Game.ElectricElements;

public abstract class BaseDelayGateElectricElement(
    SubsystemElectricity subsystemElectricity,
    CellFace cellFace
) : RotatableElectricElement(subsystemElectricity, cellFace)
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
        var inputVoltage = 0f;
        foreach (var connection in Connections)
        {
            if (connection.ConnectorType != ElectricConnectorType.Output && connection.NeighborConnectorType != 0)
            {
                inputVoltage = connection.NeighborElectricElement.GetOutputVoltage(connection.NeighborConnectorFace);
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

            if (inputVoltage.UncloseTo(_lastStoredVoltage))
            {
                _lastStoredVoltage = inputVoltage;
                if (_voltagesHistory.Count < 300)
                {
                    _voltagesHistory[SubsystemElectricity.CircuitStep + DelaySteps] = inputVoltage;
                    SubsystemElectricity.QueueElectricElementForSimulation(
                        this,
                        SubsystemElectricity.CircuitStep + DelaySteps
                    );
                }
            }
        }
        else
        {
            _voltage = inputVoltage;
        }

        return _voltage.UncloseTo(voltage);
    }
}
