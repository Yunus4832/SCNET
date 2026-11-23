using Engine.Graphics;

namespace Game.ParticleSystems;

public class MagmaSplashParticleSystem : ParticleSystem<MagmaSplashParticleSystem.Particle>
{
    public const int ParticleCount = 40;

    private readonly Vector3 _position;

    private readonly Random _random = new();

    private readonly SubsystemTerrain _subsystemTerrain;

    private float _time;

    public MagmaSplashParticleSystem(
        SubsystemTerrain terrain,
        Vector3 position,
        bool large
    ) : base(ParticleCount)
    {
        _subsystemTerrain = terrain;
        _position = position;
        Texture = AssetManager.Get<Texture2D>("Textures/MagmaSplashParticle");
        TextureSlotsCount = 2;
        SetLightOrigin(terrain, position);
        var white = Color.White;
        white.A = 255;
        var splashScale = large ? 1.5f : 1f;
        foreach (var particle in Particles)
        {
            particle.IsActive = true;
            particle.Position = position;
            particle.Color = white;
            particle.Size = new Vector2(0.2f * splashScale);
            particle.TimeToLive = particle.Duration = _random.Float(0.5f, 3.5f);
            var v = 4f *
                    _random.Float(0.1f, 1f) *
                    Vector3.Normalize(new Vector3(_random.Float(-1f, 1f), 0f, _random.Float(-1f, 1f)));
            particle.Velocity = splashScale * (v + new Vector3(0f, _random.Float(0f, 4f), 0f));
        }
    }

    public override bool Simulate(float dt)
    {
        dt = MathUtils.Clamp(dt, 0f, 0.1f);
        var velocityDamping = MathUtils.Pow(0.015f, dt);
        _time += dt;
        var hasActiveParticles = false;
        foreach (var particle in Particles)
        {
            if (!particle.IsActive)
            {
                continue;
            }

            hasActiveParticles = true;
            particle.Position += particle.Velocity * dt;
            particle.Velocity.Y += -10f * dt;
            particle.Velocity *= velocityDamping;
            particle.Color *= MathUtils.Saturate(particle.TimeToLive);
            particle.TimeToLive -= dt;
            particle.TextureSlot = (int)(3.99f * particle.TimeToLive / particle.Duration);
            particle.FlipX = _random.Sign() > 0;
            particle.FlipY = _random.Sign() > 0;
            if (particle.TimeToLive <= 0f || particle.Size.X <= 0f)
            {
                particle.IsActive = false;
                continue;
            }

            var cellValue = _subsystemTerrain.Terrain.GetCellValue(
                Terrain.ToCell(particle.Position.X),
                Terrain.ToCell(particle.Position.Y),
                Terrain.ToCell(particle.Position.Z)
            );
            var contents = Terrain.ExtractContents(cellValue);
            if (contents == 0)
            {
                continue;
            }

            var block = _subsystemTerrain.Catalog.Blocks[contents];
            if (block.Collidable)
            {
                particle.IsActive = true;
            }
            else if (block is MagmaBlock magmaBlock)
            {
                var level = FluidBlock.GetLevel(Terrain.ExtractData(cellValue));
                var levelHeight = magmaBlock.GetLevelHeight(level);
                if (!(particle.Position.Y <= MathUtils.Floor(particle.Position.Y) + levelHeight))
                {
                    continue;
                }

                particle.Velocity.Y = 0f;
                var distanceFromSplash = Vector2.Distance(
                    new Vector2(particle.Position.X, particle.Position.Z),
                    new Vector2(_position.X, _position.Z)
                );
                var waveOffset = 0.02f * MathUtils.Sin(2f * distanceFromSplash + 10f * _time);
                particle.Position.Y = MathUtils.Floor(particle.Position.Y) + levelHeight + waveOffset;
                particle.TimeToLive -= 1f * dt;
                particle.Size -= new Vector2(0.04f * dt);
            }
        }

        return !hasActiveParticles;
    }

    public class Particle : Game.ParticleSystems.Particle
    {
        public float Duration;

        public float TimeToLive;

        public Vector3 Velocity;
    }
}
