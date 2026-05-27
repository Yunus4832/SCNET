using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems;

public class SubsystemFireworksBlockBehavior : SubsystemBlockBehavior, IUpdateable
{
    private float _newYearCelebrationTimeRemaining;

    private readonly Random _random = new();

    private SubsystemAudio _subsystemAudio = null!;

    private SubsystemNoise _subsystemNoise = null!;

    private SubsystemParticles _subsystemParticles = null!;

    private SubsystemPlayers _subsystemPlayers = null!;

    private SubsystemProjectiles _subsystemProjectiles = null!;

    private SubsystemSky _subsystemSky = null!;

    private SubsystemTerrain _subsystemTerrain = null!;

    private SubsystemTime _subsystemTime = null!;

    public override int[] HandledBlocks => [];

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        var componentPlayer = _subsystemPlayers.ComponentPlayers.Count > 0
            ? _subsystemPlayers.ComponentPlayers[0]
            : null;
        if (componentPlayer == null)
        {
            return;
        }

        if (_newYearCelebrationTimeRemaining <= 0f && Time.PeriodicEvent(5.0, 0.0) &&
            _subsystemSky.SkyLightIntensity == 0f && !componentPlayer.ComponentSleep.IsSleeping)
        {
            var now = DateTime.Now;
            if (now.Year > SettingsManager.NewYearCelebrationLastYear && now.Month == 1 && now.Day == 1 &&
                now.Hour == 0 && now.Minute < 59)
            {
                SettingsManager.NewYearCelebrationLastYear = now.Year;
                _newYearCelebrationTimeRemaining = 180f;
                componentPlayer.ComponentGui.DisplayLargeMessage("Happy New Year!", "--- Enjoy the fireworks ---", 15f,
                    3f);
            }
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
            var vector2 = componentPlayer.ComponentBody.Position + new Vector3(vector.X, 0f, vector.Y);
            var terrainRaycastResult = _subsystemTerrain.Raycast(new Vector3(vector2.X, 120f, vector2.Z),
                new Vector3(vector2.X, 40f, vector2.Z), false, true, null);
            if (terrainRaycastResult.HasValue)
            {
                var data = 0;
                data = FireworksBlock.SetShape(data, (FireworksBlock.Shape)_random.Int(0, 7));
                data = FireworksBlock.SetColor(data, _random.Int(0, 7));
                data = FireworksBlock.SetAltitude(data, _random.Int(0, 1));
                data = FireworksBlock.SetFlickering(data, _random.Float(0f, 1f) < 0.25f);
                var value = Terrain.MakeBlockValue(215, 0, data);
                var position = new Vector3(terrainRaycastResult.Value.CellFace.Point.X,
                    terrainRaycastResult.Value.CellFace.Point.Y + 1, terrainRaycastResult.Value.CellFace.Point.Z);
                _subsystemProjectiles.FireProjectile(value, position,
                    new Vector3(_random.Float(-3f, 3f), 45f, _random.Float(-3f, 3f)), Vector3.Zero, null);
            }
        }
    }

    public void ExplodeFireworks(Vector3 position, int data)
    {
        for (var i = 0; i < 3; i++)
        {
            var v = new Vector3(_random.Float(-3f, 3f), -15f, _random.Float(-3f, 3f));
            if (_subsystemTerrain.Raycast(position, position + v, false, true, null).HasValue)
            {
                return;
            }
        }

        var shape = FireworksBlock.GetShape(data);
        var flickering = FireworksBlock.GetFlickering(data) ? 0.66f : 0f;
        var particleSize = FireworksBlock.GetAltitude(data) > 0 ? 1.1f : 1f;
        var color = FireworksBlock.FireworksColors[FireworksBlock.GetColor(data)];
        _subsystemParticles.AddParticleSystem(new FireworksParticleSystem(position, color, shape, flickering,
            particleSize));
        _subsystemAudio.PlayRandomSound("Audio/FireworksPop", 1f, _random.Float(-0.4f, 0f), position, 80f, true);
        _subsystemNoise.MakeNoise(position, 1f, 60f);
    }

    public override void OnFiredAsProjectile(Projectile projectile)
    {
        var data = Terrain.ExtractData(projectile.Value);
        var num = FireworksBlock.GetAltitude(data) == 0 ? 0.8f : 1.3f;
        _subsystemProjectiles.AddTrail(projectile, Vector3.Zero, new FireworksTrailParticleSystem());
        _subsystemAudio.PlayRandomSound("Audio/FireworksWhoosh", 1f, _random.Float(-0.2f, 0.2f), projectile.Position,
            8f, true);
        _subsystemNoise.MakeNoise(projectile.Position, 1f, 10f);
        _subsystemTime.QueueGameTimeDelayedExecution(_subsystemTime.GameTime + num, delegate
        {
            if (projectile.ToRemove)
            {
                return;
            }

            projectile.ToRemove = true;
            ExplodeFireworks(projectile.Position, data);
        });
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
        _subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true)!;
        _subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true)!;
        _subsystemNoise = Project.FindSubsystem<SubsystemNoise>(true)!;
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _subsystemSky = Project.FindSubsystem<SubsystemSky>(true)!;
        _subsystemPlayers = Project.FindSubsystem<SubsystemPlayers>(true)!;
    }
}
