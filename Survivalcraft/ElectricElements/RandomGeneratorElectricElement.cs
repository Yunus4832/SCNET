using Game.NetWork;

namespace Game.ElectricElements;

public class RandomGeneratorElectricElement : RotateableElectricElement
{
    private static readonly Random _innerRandom = new();

    private bool _clockAllowed = true;

    private float _voltage;

    public RandomGeneratorElectricElement(
        SubsystemElectricity subsystemElectricity,
        CellFace cellFace
    ) : base(subsystemElectricity, cellFace)
    {
        var num = SubsystemElectricity.ReadPersistentVoltage(CellFaces[0].Point);
        _voltage = num ?? GetRandomVoltage();
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
                        flag = true;
                        _clockAllowed = false;
                    }
                }
                else
                {
                    _clockAllowed = true;
                }

                flag2 = true;
            }
        }

        if (flag2)
        {
            if (flag)
            {
                _voltage = GetRandomVoltage();
            }
        }
        else
        {
            _voltage = GetRandomVoltage();
            SubsystemElectricity.QueueElectricElementForSimulation(this,
                SubsystemElectricity.CircuitStep + MathUtils.Max((int)(_innerRandom.Float(0.25f, 0.75f) / 0.01f), 1));
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
        if (CommonLib.WorkType != WorkType.Client)
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
}
