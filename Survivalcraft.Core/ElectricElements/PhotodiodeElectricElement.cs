namespace Game.ElectricElements;

public class PhotodiodeElectricElement : MountedElectricElement
{
    private float _voltage;

    public PhotodiodeElectricElement(
        SubsystemElectricity subsystemElectricity,
        CellFace cellFace
    ) : base(subsystemElectricity, cellFace)
    {
        _voltage = CalculateVoltage();
    }

    public override float GetOutputVoltage(int face)
    {
        return _voltage;
    }

    public override bool Simulate()
    {
        var voltage = _voltage;
        _voltage = CalculateVoltage();
        SubsystemElectricity.QueueElectricElementForSimulation(
            this,
            SubsystemElectricity.CircuitStep + MathUtils.Max(50, 1)
        );
        return _voltage.UncloseTo(voltage);
    }

    public float CalculateVoltage()
    {
        var cellFace = CellFaces[0];
        var point = CellFace.FaceToPoint3(cellFace.Face);
        var terrain = SubsystemElectricity.SubsystemTerrain;
        var environment = terrain.CaptureLightEnvironment();
        if (!terrain.Terrain.Lighting.TryGetEffectiveLight(
                cellFace.X,
                cellFace.Y,
                cellFace.Z,
                environment,
                out var cellLight
            ) ||
            !terrain.Terrain.Lighting.TryGetEffectiveLight(
                cellFace.X + point.X,
                cellFace.Y + point.Y,
                cellFace.Z + point.Z,
                environment,
                out var cellLight2
            ))
        {
            return _voltage;
        }

        return MathUtils.Max(cellLight, cellLight2) / 15f;
    }
}
