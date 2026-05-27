using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems;

public class SubsystemImpactExplosivesBlockBehavior : SubsystemBlockBehavior
{
    private SubsystemExplosions _subsystemExplosions = null!;

    public override int[] HandledBlocks => [];

    public override bool OnHitAsProjectile(CellFace? cellFace, ComponentBody componentBody, WorldItem worldItem)
    {
        return _subsystemExplosions.TryExplodeBlock(Terrain.ToCell(worldItem.Position.X),
            Terrain.ToCell(worldItem.Position.Y), Terrain.ToCell(worldItem.Position.Z), worldItem.Value);
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        base.Load(valuesDictionary);
        _subsystemExplosions = Project.FindSubsystem<SubsystemExplosions>(true)!;
    }
}
