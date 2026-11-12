using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems.Behavior;

public class SubsystemFireworksBlockBehavior : SubsystemBlockBehavior, IUpdateable
{
    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Authority;

    private float _newYearCelebrationTimeRemaining;

    private readonly Random _random = new();

    private SubsystemNoise _subsystemNoise = null!;

    private SubsystemCharacters _subsystemCharacters = null!;

    private SubsystemProjectiles _subsystemProjectiles = null!;

    private SubsystemSky _subsystemSky = null!;

    private SubsystemTerrain _subsystemTerrain = null!;

    private SubsystemTime _subsystemTime = null!;

    private bool _disposed;

    public bool IsNewYearCelebrationActive => _newYearCelebrationTimeRemaining > 0f;

    public ComponentCharacter? TryStartNewYearCelebration()
    {
        if (_disposed ||
            !WorldRuntimeContext.IsProjectActive(Project) ||
            !WorldRuntimeContext.For(Project).IsAuthority ||
            IsNewYearCelebrationActive ||
            _subsystemSky.SkyLightIntensity != 0f ||
            _subsystemCharacters.ComponentCharacters.Count == 0)
        {
            return null;
        }

        var character = _subsystemCharacters.ComponentCharacters[0];
        if (character.ComponentSleep.IsSleeping) { return null; }

        _newYearCelebrationTimeRemaining = 180f;
        return character;
    }

    public override ResourceId[] HandledBlocks => [];

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        if (_disposed || !WorldRuntimeContext.IsProjectActive(Project) || !WorldRuntimeContext.For(Project).IsAuthority)
        {
            return;
        }

        var componentCharacter = _subsystemCharacters.ComponentCharacters.Count > 0
            ? _subsystemCharacters.ComponentCharacters[0]
            : null;
        if (componentCharacter == null)
        {
            return;
        }

        if (!(_newYearCelebrationTimeRemaining > 0f))
        {
            return;
        }

        _newYearCelebrationTimeRemaining -= dt;
        var num = _newYearCelebrationTimeRemaining > 10f
            ? MathUtils.Lerp(1f, 7f, 0.5f * MathUtils.Sin(0.25f * _newYearCelebrationTimeRemaining) + 0.5f)
            : 20f;
        if (_random.Float(0f, 1f) < num * dt)
        {
            var vector = _random.Vector2(35f, 50f);
            var vector2 = componentCharacter.ComponentBody.Position + new Vector3(vector.X, 0f, vector.Y);
            var terrainRaycastResult = _subsystemTerrain.Raycast(
                new Vector3(vector2.X, 120f, vector2.Z),
                new Vector3(vector2.X, 40f, vector2.Z),
                false,
                true,
                null
            );
            if (terrainRaycastResult.HasValue)
            {
                var data = 0;
                data = FireworksBlock.SetShape(data, (FireworksBlock.Shape)_random.Int(0, 7));
                data = FireworksBlock.SetColor(data, _random.Int(0, 7));
                data = FireworksBlock.SetAltitude(data, _random.Int(0, 1));
                data = FireworksBlock.SetFlickering(data, _random.Float(0f, 1f) < 0.25f);
                var value = Terrain.MakeBlockValue(215, data);
                var position = new Vector3(
                    terrainRaycastResult.Value.CellFace.Point.X,
                    terrainRaycastResult.Value.CellFace.Point.Y + 1,
                    terrainRaycastResult.Value.CellFace.Point.Z
                );
                _subsystemProjectiles.FireProjectile(
                    value,
                    position,
                    new Vector3(_random.Float(-3f, 3f), 45f, _random.Float(-3f, 3f)),
                    Vector3.Zero,
                    null
                );
            }
        }
    }

    public void ExplodeFireworks(Vector3 position, int data)
    {
        if (_disposed || !WorldRuntimeContext.IsProjectActive(Project) || !WorldRuntimeContext.For(Project).IsAuthority)
        {
            return;
        }

        for (var i = 0; i < 3; i++)
        {
            var v = new Vector3(_random.Float(-3f, 3f), -15f, _random.Float(-3f, 3f));
            if (_subsystemTerrain.Raycast(position, position + v, false, true, null).HasValue)
            {
                return;
            }
        }

        var pitch = _random.Float(-0.4f, 0f);
        _subsystemProjectiles.PublishFireworksEffect(
            ProjectilePackage.EffectType.FireworksExplosion,
            data & 255,
            position,
            pitch
        );
        _subsystemNoise.MakeNoise(position, 1f, 60f);
    }

    public override void OnFiredAsProjectile(Projectile projectile)
    {
        if (_disposed || !WorldRuntimeContext.IsProjectActive(Project)) { return; }

        _subsystemProjectiles.SetTrail(
            projectile,
            new ProjectileTrailDescription(
                ProjectileTrailKind.Fireworks,
                Vector3.Zero,
                60,
                0f,
                float.MaxValue,
                Color.White
            )
        );
        if (!WorldRuntimeContext.For(Project).IsAuthority) { return; }

        var data = Terrain.ExtractData(projectile.Value);
        var num = FireworksBlock.GetAltitude(data) == 0 ? 0.8f : 1.3f;
        var pitch = _random.Float(-0.2f, 0.2f);
        _subsystemProjectiles.PublishFireworksEffect(
            ProjectilePackage.EffectType.FireworksWhoosh,
            data & 255,
            projectile.Position,
            pitch
        );
        _subsystemNoise.MakeNoise(projectile.Position, 1f, 10f);
        _subsystemTime.QueueGameTimeDelayedExecution(
            _subsystemTime.GameTime + num,
            delegate
            {
                if (_disposed || !WorldRuntimeContext.IsProjectActive(Project) || projectile.ToRemove)
                {
                    return;
                }

                projectile.ToRemove = true;
                ExplodeFireworks(projectile.Position, data);
            }
        );
    }

    public override bool OnHitAsProjectile(CellFace? cellFace, ComponentBody componentBody, WorldItem worldItem)
    {
        return true;
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        base.Load(valuesDictionary);
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _subsystemProjectiles = Project.FindSubsystem<SubsystemProjectiles>(true)!;
        _subsystemNoise = Project.FindSubsystem<SubsystemNoise>(true)!;
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _subsystemSky = Project.FindSubsystem<SubsystemSky>(true)!;
        _subsystemCharacters = Project.FindSubsystem<SubsystemCharacters>(true)!;
    }

    public override void Dispose()
    {
        _disposed = true;
        _newYearCelebrationTimeRemaining = 0f;
        base.Dispose();
    }
}
