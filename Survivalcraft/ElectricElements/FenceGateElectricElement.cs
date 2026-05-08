namespace Game.ElectricElements;

public class FenceGateElectricElement : ElectricElement
{
    private int _lastChangeCircuitStep;

    private bool _needsReset;

    private float _voltage;

    public FenceGateElectricElement(
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
            SubsystemElectricity.Project.FindSubsystem<SubsystemFenceGateBlockBehavior>(true)!
                .OpenCloseGate(cellFace.X, cellFace.Y, cellFace.Z, !FenceGateBlock.GetOpen(data));
        }
        else
        {
            SubsystemElectricity.QueueElectricElementForSimulation(this,
                SubsystemElectricity.CircuitStep + 10 - num);
        }

        return false;
    }
}
