namespace Game.ElectricElements;

public class ThermometerElectricElement : ElectricElement
{
    private const float _pollingPeriod = 0.5f;

    private readonly SubsystemMetersBlockBehavior _subsystemMetersBlockBehavior;

    private float _voltage;

    public ThermometerElectricElement(
        SubsystemElectricity subsystemElectricity,
        CellFace cellFace
    ) : base(subsystemElectricity, cellFace)
    {
        _subsystemMetersBlockBehavior = SubsystemElectricity.Project.FindSubsystem<SubsystemMetersBlockBehavior>(true)!;
    }

    public override float GetOutputVoltage(int face)
    {
        return _voltage;
    }

    public override bool Simulate()
    {
        var voltage = _voltage;
        var cellFace = CellFaces[0];
        _voltage = MathUtils.Saturate(
            _subsystemMetersBlockBehavior.GetThermometerReading(cellFace.X, cellFace.Y, cellFace.Z) / 15f);
        var num = 0.5f * (0.9f + 0.000200000009f * (GetHashCode() % 1000));
        SubsystemElectricity.QueueElectricElementForSimulation(this,
            SubsystemElectricity.CircuitStep + MathUtils.Max((int)(num / 0.01f), 1));
        return _voltage.UncloseTo(voltage);
    }
}
