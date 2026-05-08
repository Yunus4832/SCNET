namespace Game.ElectricElements;

public class DelayGateElectricElement(
    SubsystemElectricity subsystemElectricity,
    CellFace cellFace
) : BaseDelayGateElectricElement(subsystemElectricity, cellFace)
{
    private static readonly int[] _delaysByPredecessorsCount =
    [
        20,
        80,
        400
    ];

    private int? _delaySteps;

    private int _lastDelayCalculationStep;

    protected override int DelaySteps
    {
        get
        {
            if (SubsystemElectricity.CircuitStep - _lastDelayCalculationStep > 50)
            {
                _delaySteps = null;
            }

            if (_delaySteps.HasValue)
            {
                return _delaySteps.Value;
            }

            var count = 0;
            CountDelayPredecessors(this, ref count);
            _delaySteps = _delaysByPredecessorsCount[count];
            _lastDelayCalculationStep = SubsystemElectricity.CircuitStep;

            return _delaySteps.Value;
        }
    }

    public static void CountDelayPredecessors(DelayGateElectricElement delayGate, ref int count)
    {
        if (count >= 2)
        {
            return;
        }

        foreach (var connection in delayGate.Connections)
        {
            if (connection.ConnectorType != ElectricConnectorType.Input)
            {
                continue;
            }

            if (connection.NeighborElectricElement is not DelayGateElectricElement delayGateElectricElement)
            {
                continue;
            }

            count++;
            CountDelayPredecessors(delayGateElectricElement, ref count);
            break;
        }
    }
}
