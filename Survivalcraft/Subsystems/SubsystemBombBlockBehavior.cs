using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems;

public class SubsystemBombBlockBehavior : SubsystemBlockBehavior, IUpdateable
{
    private readonly Dictionary<Projectile, bool> _projectiles = new();

    private SubsystemBlockBehaviors _subsystemBlockBehaviors = null!;

    private SubsystemExplosions _subsystemExplosions = null!;

    private SubsystemGameInfo _subsystemGameInfo = null!;

    private SubsystemProjectiles _subsystemProjectiles = null!;

    private SubsystemTime _subsystemTime = null!;

    public override int[] HandledBlocks => [];

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        if (!_subsystemTime.PeriodicGameTimeEvent(0.1, 0.0))
        {
            return;
        }

        foreach (var key in _projectiles.Keys)
        {
            if (_subsystemGameInfo.TotalElapsedGameTime - key.CreationTime > 5.0)
            {
                ComponentPlayer? player = null;
                if (key.Owner != null)
                {
                    player = key.Owner.Entity.FindComponent<ComponentPlayer>();
                }

                _subsystemExplosions.TryExplodeBlock(Terrain.ToCell(key.Position.X),
                    Terrain.ToCell(key.Position.Y), Terrain.ToCell(key.Position.Z), key.Value,
                    player?.PlayerData);
                key.ToRemove = true;
            }
        }
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        base.Load(valuesDictionary);
        _subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true)!;
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _subsystemExplosions = Project.FindSubsystem<SubsystemExplosions>(true)!;
        _subsystemProjectiles = Project.FindSubsystem<SubsystemProjectiles>(true)!;
        _subsystemBlockBehaviors = Project.FindSubsystem<SubsystemBlockBehaviors>(true)!;
        foreach (var projectile in _subsystemProjectiles.Projectiles)
        {
            ScanProjectile(projectile);
        }

        _subsystemProjectiles.ProjectileAdded += ScanProjectile;
        _subsystemProjectiles.ProjectileRemoved += delegate(Projectile projectile) { _projectiles.Remove(projectile); };
    }

    private void ScanProjectile(Projectile projectile)
    {
        if (_projectiles.ContainsKey(projectile))
        {
            return;
        }

        var num = Terrain.ExtractContents(projectile.Value);
        if (!_subsystemBlockBehaviors.GetBlockBehaviors(num).Contains(this))
        {
            return;
        }

        _projectiles.Add(projectile, true);
        projectile.ProjectileStoppedAction = ProjectileStoppedAction.DoNothing;
        var color = num == 228 ? new Color(255, 140, 192) : Color.White;
        _subsystemProjectiles.AddTrail(projectile, new Vector3(0f, 0.25f, 0.1f),
            new SmokeTrailParticleSystem(20, 0.33f, float.MaxValue, color));
    }
}
