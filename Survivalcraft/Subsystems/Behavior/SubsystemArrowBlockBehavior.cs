using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems;

public class SubsystemArrowBlockBehavior : SubsystemBlockBehavior
{
    private readonly Random _random = new();

    private SubsystemProjectiles _subsystemProjectiles = null!;

    public override int[] HandledBlocks => [];

    public override void OnFiredAsProjectile(Projectile projectile)
    {
        if (ArrowBlock.GetArrowType(Terrain.ExtractData(projectile.Value)) != ArrowBlock.ArrowType.FireArrow)
        {
            return;
        }

        _subsystemProjectiles.AddTrail(projectile, Vector3.Zero,
            new SmokeTrailParticleSystem(20, 0.5f, float.MaxValue, Color.White));
        projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
        projectile.IsIncendiary = true;
    }

    public override bool OnHitAsProjectile(CellFace? cellFace, ComponentBody componentBody, WorldItem worldItem)
    {
        var arrowType = ArrowBlock.GetArrowType(Terrain.ExtractData(worldItem.Value));
        if (!(worldItem.Velocity.Length() > 10f))
        {
            return false;
        }

        var num = arrowType switch
        {
            ArrowBlock.ArrowType.FireArrow => 0.5f,
            ArrowBlock.ArrowType.WoodenArrow => 0.2f,
            ArrowBlock.ArrowType.DiamondArrow => 0f,
            ArrowBlock.ArrowType.IronBolt => 0.05f,
            ArrowBlock.ArrowType.DiamondBolt => 0f,
            _ => 0.1f
        };
        return _random.Float(0f, 1f) < num;
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        base.Load(valuesDictionary);
        _subsystemProjectiles = Project.FindSubsystem<SubsystemProjectiles>(true)!;
    }
}
