namespace Game.ElectricElements;

public class TargetElectricElement(
    SubsystemElectricity subsystemElectricity,
    CellFace cellFace
) : MountedElectricElement(subsystemElectricity, cellFace)
{
    private int _score;

    private float _voltage;

    public override float GetOutputVoltage(int face)
    {
        return _voltage;
    }

    public override bool Simulate()
    {
        var voltage = _voltage;
        if (_score > 0)
        {
            _voltage = (_score + 7) / 15f;
            _score = 0;
            SubsystemElectricity.QueueElectricElementForSimulation(this, SubsystemElectricity.CircuitStep + 50);
        }
        else
        {
            _voltage = 0f;
        }

        return _voltage.UncloseTo(voltage);
    }

    public override void OnHitByProjectile(CellFace cellFace, WorldItem worldItem)
    {
        if (_score != 0 || IsSignalHigh(_voltage))
        {
            return;
        }

        if (cellFace.Face is 0 or 2)
        {
            var num = worldItem.Position.X - cellFace.X - 0.5f;
            var num2 = worldItem.Position.Y - cellFace.Y - 0.5f;
            var num3 = MathUtils.Sqrt(num * num + num2 * num2);
            _score = MathUtils.Clamp((int)MathUtils.Round(8f * (1f - num3 / 0.707f)), 1, 8);
        }
        else
        {
            var num4 = worldItem.Position.Z - cellFace.Z - 0.5f;
            var num5 = worldItem.Position.Y - cellFace.Y - 0.5f;
            var num6 = MathUtils.Sqrt(num4 * num4 + num5 * num5);
            _score = MathUtils.Clamp((int)MathUtils.Round(8f * (1f - num6 / 0.5f)), 1, 8);
        }

        SubsystemElectricity.QueueElectricElementForSimulation(this, SubsystemElectricity.CircuitStep + 1);
    }
}
