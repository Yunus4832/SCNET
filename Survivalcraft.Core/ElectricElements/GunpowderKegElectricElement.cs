namespace Game.ElectricElements;

public class GunpowderKegElectricElement(
    SubsystemElectricity subsystemElectricity,
    CellFace cellFace
) : ElectricElement(subsystemElectricity, cellFace)
{
    public override bool Simulate()
    {
        if (CalculateHighInputsCount() <= 0)
        {
            return false;
        }

        var cellFace = CellFaces[0];
        SubsystemElectricity.Project.FindSubsystem<SubsystemExplosivesBlockBehavior>(true)!
            .IgniteFuse(cellFace.X, cellFace.Y, cellFace.Z);

        return false;
    }
}
