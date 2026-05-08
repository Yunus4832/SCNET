namespace Game.ElectricElements;

public class HygrometerElectricElement(
    SubsystemElectricity subsystemElectricity,
    CellFace cellFace
) : ElectricElement(subsystemElectricity, cellFace)
{
    private float _voltage;

    public override float GetOutputVoltage(int face)
    {
        return _voltage;
    }

    public override bool Simulate()
    {
        var voltage = _voltage;
        var cellFace = CellFaces[0];
        var humidity = SubsystemElectricity.SubsystemTerrain.Terrain.GetHumidity(cellFace.X, cellFace.Z);
        _voltage = humidity / 15f;
        return _voltage.UncloseTo(voltage);
    }
}
