namespace Game.ElectricElements;

public class DetonatorElectricElement(
    SubsystemElectricity subsystemElectricity,
    CellFace cellFace
) : MountedElectricElement(subsystemElectricity, cellFace)
{
    public void Detonate()
    {
        var cellFace = CellFaces[0];
        var value = Terrain.MakeBlockValue(147);
        SubsystemElectricity.Project.FindSubsystem<SubsystemExplosions>()?
            .TryExplodeBlock(cellFace.X, cellFace.Y, cellFace.Z, value);
    }

    public override bool Simulate()
    {
        if (CalculateHighInputsCount() > 0)
        {
            Detonate();
        }

        return false;
    }

    public override void OnHitByProjectile(CellFace cellFace, WorldItem worldItem)
    {
        Detonate();
    }
}
