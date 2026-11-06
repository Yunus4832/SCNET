namespace Game.ElectricElements;

public class AdjustableDelayGateElectricElement : BaseDelayGateElectricElement
{
    private readonly int _delaySteps;

    public AdjustableDelayGateElectricElement(
        SubsystemElectricity subsystemElectricity,
        CellFace cellFace
    ) : base(subsystemElectricity, cellFace)
    {
        var data = Terrain.ExtractData(
            subsystemElectricity.SubsystemTerrain.Terrain.GetCellValue(
                cellFace.X,
                cellFace.Y,
                cellFace.Z
            )
        );
        _delaySteps = AdjustableDelayGateBlock.GetDelay(data);
    }

    protected override int DelaySteps => _delaySteps;
}
