namespace Game.ElectricElements;

public class SwitchFurnitureElectricElement : FurnitureElectricElement
{
    private readonly float _voltage;

    public SwitchFurnitureElectricElement(
        SubsystemElectricity subsystemElectricity,
        Point3 point,
        int value
    ) : base(subsystemElectricity, point)
    {
        var design =
            FurnitureBlock.GetDesign(subsystemElectricity.SubsystemTerrain.SubsystemFurnitureBlockBehavior, value);
        if (design is { LinkedDesign: not null })
        {
            _voltage = design.Index >= design.LinkedDesign.Index ? 1 : 0;
        }
    }

    public override float GetOutputVoltage(int face)
    {
        return _voltage;
    }

    public override bool OnInteract(TerrainRaycastResult raycastResult, ComponentMiner componentMiner)
    {
        var cellFace = CellFaces[0];
        SubsystemElectricity.SubsystemTerrain.SubsystemFurnitureBlockBehavior.SwitchToNextState(cellFace.X, cellFace.Y,
            cellFace.Z, false);
        SubsystemElectricity.SubsystemAudio.PlaySound("Audio/Click", 1f, 0f,
            new Vector3(cellFace.X, cellFace.Y, cellFace.Z), 2f, true);
        return true;
    }
}
