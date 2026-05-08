namespace Game.ElectricElements;

public class ChristmasTreeElectricElement : ElectricElement
{
    private int _lastChangeCircuitStep;

    private float _voltage;

    public ChristmasTreeElectricElement(
        SubsystemElectricity subsystemElectricity,
        CellFace cellFace,
        int value
    ) : base(subsystemElectricity, cellFace)
    {
        _lastChangeCircuitStep = SubsystemElectricity.CircuitStep;
        _voltage = ChristmasTreeBlock.GetLightState(Terrain.ExtractData(value)) ? 1 : 0;
    }

    public override bool Simulate()
    {
        var num = SubsystemElectricity.CircuitStep - _lastChangeCircuitStep;
        float voltage = CalculateHighInputsCount() > 0 ? 1 : 0;
        if (IsSignalHigh(voltage) != IsSignalHigh(_voltage))
        {
            _lastChangeCircuitStep = SubsystemElectricity.CircuitStep;
        }

        _voltage = voltage;
        if (num >= 10)
        {
            var cellFace = CellFaces[0];
            var cellValue =
                SubsystemElectricity.SubsystemTerrain.Terrain.GetCellValue(cellFace.X, cellFace.Y, cellFace.Z);
            var data = ChristmasTreeBlock.SetLightState(Terrain.ExtractData(cellValue), IsSignalHigh(_voltage));
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
