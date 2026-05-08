namespace Game.ElectricElements;

public class LightBulbElectricElement : MountedElectricElement
{
    private int _intensity;

    private int _lastChangeCircuitStep;

    public LightBulbElectricElement(
        SubsystemElectricity subsystemElectricity,
        CellFace cellFace,
        int value
    ) : base(subsystemElectricity, cellFace)
    {
        _lastChangeCircuitStep = SubsystemElectricity.CircuitStep;
        var data = Terrain.ExtractData(value);
        _intensity = LightbulbBlock.GetLightIntensity(data);
    }

    public override bool Simulate()
    {
        var num = SubsystemElectricity.CircuitStep - _lastChangeCircuitStep;
        var num2 = 0f;
        foreach (var connection in Connections)
        {
            if (connection.ConnectorType != ElectricConnectorType.Output && connection.NeighborConnectorType != 0)
            {
                num2 = MathUtils.Max(num2,
                    connection.NeighborElectricElement.GetOutputVoltage(connection.NeighborConnectorFace));
            }
        }

        var intensity = _intensity;
        _intensity = MathUtils.Clamp((int)MathUtils.Round((num2 - 0.5f) * 30f), 0, 15);
        if (_intensity != intensity)
        {
            _lastChangeCircuitStep = SubsystemElectricity.CircuitStep;
        }

        if (num >= 10)
        {
            var cellFace = CellFaces[0];
            var cellValue =
                SubsystemElectricity.SubsystemTerrain.Terrain.GetCellValue(cellFace.X, cellFace.Y, cellFace.Z);
            var data = LightbulbBlock.SetLightIntensity(Terrain.ExtractData(cellValue), _intensity);
            var value = Terrain.ReplaceData(cellValue, data);
            SubsystemElectricity.SubsystemTerrain.ChangeCell(cellFace.X, cellFace.Y, cellFace.Z, value);
        }
        else
        {
            SubsystemElectricity.QueueElectricElementForSimulation(this, SubsystemElectricity.CircuitStep + 10 - num);
        }

        return false;
    }
}
