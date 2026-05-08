namespace Game.ElectricElements;

public class TrapDoorElectricElement : ElectricElement
{
    private int _lastChangeCircuitStep;

    private bool _needsReset;

    private float _voltage;

    public TrapDoorElectricElement(
        SubsystemElectricity subsystemElectricity,
        CellFace cellFace
    ) : base(subsystemElectricity, cellFace)
    {
        _lastChangeCircuitStep = SubsystemElectricity.CircuitStep;
        _needsReset = true;
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
        if (!IsSignalHigh(_voltage))
        {
            _needsReset = false;
        }

        if (_needsReset)
        {
            return false;
        }

        if (num >= 10)
        {
            if (!IsSignalHigh(_voltage))
            {
                return false;
            }

            var cellFace = CellFaces[0];
            var data = Terrain.ExtractData(
                SubsystemElectricity.SubsystemTerrain.Terrain.GetCellValue(cellFace.X, cellFace.Y, cellFace.Z));
            SubsystemElectricity.Project.FindSubsystem<SubsystemTrapdoorBlockBehavior>(true)!
                .OpenCloseTrapdoor(cellFace.X, cellFace.Y, cellFace.Z, !TrapdoorBlock.GetOpen(data));
        }
        else
        {
            SubsystemElectricity.QueueElectricElementForSimulation(this,
                SubsystemElectricity.CircuitStep + 10 - num);
        }

        return false;
    }
}
