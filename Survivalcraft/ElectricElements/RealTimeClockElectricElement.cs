namespace Game.ElectricElements;

public class RealTimeClockElectricElement : RotateableElectricElement
{
    private const int _periodsPerDay = 4096;

    private int _lastClockValue = -1;

    private readonly SubsystemTimeOfDay _subsystemTimeOfDay;

    public RealTimeClockElectricElement(
        SubsystemElectricity subsystemElectricity,
        CellFace cellFace
    ) : base(subsystemElectricity, cellFace)
    {
        _subsystemTimeOfDay = SubsystemElectricity.Project.FindSubsystem<SubsystemTimeOfDay>(true)!;
    }

    public override float GetOutputVoltage(int face)
    {
        var connectorDirection = SubsystemElectricity.GetConnectorDirection(CellFaces[0].Face, Rotation, face);
        return connectorDirection switch
        {
            null => 0f,
            ElectricConnectorDirection.Top => (GetClockValue() & 0xF) / 15f,
            ElectricConnectorDirection.Right => ((GetClockValue() >> 4) & 0xF) / 15f,
            ElectricConnectorDirection.Bottom => ((GetClockValue() >> 8) & 0xF) / 15f,
            ElectricConnectorDirection.Left => ((GetClockValue() >> 12) & 0xF) / 15f,
            ElectricConnectorDirection.In => ((GetClockValue() >> 16) & 0xF) / 15f,
            _ => 0f
        };
    }

    public override bool Simulate()
    {
        var day = _subsystemTimeOfDay.Day;
        var num = (int)(((MathUtils.Ceiling(day * _periodsPerDay) + 0.5) / _periodsPerDay - day) *
            _subsystemTimeOfDay.DayDuration / SubsystemElectricity.CircuitStepDuration);
        var circuitStep = MathUtils.Max(SubsystemElectricity.FrameStartCircuitStep + num,
            SubsystemElectricity.CircuitStep + 1);
        SubsystemElectricity.QueueElectricElementForSimulation(this, circuitStep);
        var clockValue = GetClockValue();
        if (clockValue == _lastClockValue)
        {
            return false;
        }

        _lastClockValue = clockValue;
        return true;
    }

    public int GetClockValue()
    {
        return (int)(_subsystemTimeOfDay.Day * _periodsPerDay);
    }
}
