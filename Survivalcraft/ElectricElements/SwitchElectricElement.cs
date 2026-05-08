namespace Game.ElectricElements;

public class SwitchElectricElement : MountedElectricElement
{
    private readonly float _voltage;

    public SwitchElectricElement(
        SubsystemElectricity subsystemElectricity,
        CellFace cellFace, int value
    ) : base(subsystemElectricity, cellFace)
    {
        var voltageLevel = SwitchBlock.GetVoltageLevel(Terrain.ExtractData(value));
        _voltage = SwitchBlock.GetLeverState(value) ? voltageLevel / 15f : 0f;
    }

    public override float GetOutputVoltage(int face)
    {
        return _voltage;
    }

    public override bool OnInteract(TerrainRaycastResult raycastResult, ComponentMiner componentMiner)
    {
        var cellFace = CellFaces[0];
        var cellValue = SubsystemElectricity.SubsystemTerrain.Terrain.GetCellValue(cellFace.X, cellFace.Y, cellFace.Z);
        var value = SwitchBlock.SetLeverState(cellValue, !SwitchBlock.GetLeverState(cellValue));
        SubsystemElectricity.SubsystemTerrain.ChangeCell(cellFace.X, cellFace.Y, cellFace.Z, value);
        SubsystemElectricity.SubsystemAudio.PlaySound("Audio/Click", 1f, 0f,
            new Vector3(cellFace.X, cellFace.Y, cellFace.Z), 2f, true);
        return true;
    }
}
