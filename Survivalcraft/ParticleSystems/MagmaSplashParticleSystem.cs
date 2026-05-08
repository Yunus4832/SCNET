using Engine.Graphics;

namespace Game.ParticleSystems;

public class MagmaSplashParticleSystem : ParticleSystem<MagmaSplashParticleSystem.Particle>
{
    private readonly Vector3 _position;

    private readonly Random _random = new();

    private readonly SubsystemTerrain _subsystemTerrain;

    private float _time;

    public MagmaSplashParticleSystem(
        SubsystemTerrain terrain,
        Vector3 position,
        bool large
    ) : base(40)
    {
        _subsystemTerrain = terrain;
        _position = position;
        Texture = ContentManager.Get<Texture2D>("Textures/MagmaSplashParticle");
        TextureSlotsCount = 2;
        var num = Terrain.ToCell(position.X);
        var num2 = Terrain.ToCell(position.Y);
        var num3 = Terrain.ToCell(position.Z);
        var x = 0;
        x = MathUtils.Max(x, terrain.Terrain.GetCellLight(num + 1, num2, num3));
        x = MathUtils.Max(x, terrain.Terrain.GetCellLight(num - 1, num2, num3));
        x = MathUtils.Max(x, terrain.Terrain.GetCellLight(num, num2 + 1, num3));
        x = MathUtils.Max(x, terrain.Terrain.GetCellLight(num, num2 - 1, num3));
        x = MathUtils.Max(x, terrain.Terrain.GetCellLight(num, num2, num3 + 1));
        x = MathUtils.Max(x, terrain.Terrain.GetCellLight(num, num2, num3 - 1));
        var white = Color.White;
        var num4 = LightingManager.LightIntensityByLightValue[x];
        white *= num4;
        white.A = 255;
        var num5 = large ? 1.5f : 1f;
        foreach (var particle in Particles)
        {
            particle.IsActive = true;
            particle.Position = position;
            particle.Color = white;
            particle.Size = new Vector2(0.2f * num5);
            particle.TimeToLive = particle.Duration = _random.Float(0.5f, 3.5f);
            var v = 4f * _random.Float(0.1f, 1f) *
                    Vector3.Normalize(new Vector3(_random.Float(-1f, 1f), 0f, _random.Float(-1f, 1f)));
            particle.Velocity = num5 * (v + new Vector3(0f, _random.Float(0f, 4f), 0f));
        }
    }

    public override bool Simulate(float dt)
    {
        dt = MathUtils.Clamp(dt, 0f, 0.1f);
        var num = MathUtils.Pow(0.015f, dt);
        _time += dt;
        var flag = false;
        foreach (var particle in Particles)
        {
            if (!particle.IsActive)
            {
                continue;
            }

            flag = true;
            particle.Position += particle.Velocity * dt;
            particle.Velocity.Y += -10f * dt;
            particle.Velocity *= num;
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

            var cellValue = _subsystemTerrain.Terrain.GetCellValue(Terrain.ToCell(particle.Position.X),
                Terrain.ToCell(particle.Position.Y), Terrain.ToCell(particle.Position.Z));
            var num2 = Terrain.ExtractContents(cellValue);
            if (num2 == 0)
            {
                continue;
            }

            var block = BlocksManager.Blocks[num2];
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
                var num3 = Vector2.Distance(
                    new Vector2(particle.Position.X, particle.Position.Z),
                    new Vector2(_position.X, _position.Z)
                );
                var num4 = 0.02f * MathUtils.Sin(2f * num3 + 10f * _time);
                particle.Position.Y = MathUtils.Floor(particle.Position.Y) + levelHeight + num4;
                particle.TimeToLive -= 1f * dt;
                particle.Size -= new Vector2(0.04f * dt);
            }
        }

        return !flag;
    }

    public class Particle : Game.Particle
    {
        public float Duration;

        public float TimeToLive;

        public Vector3 Velocity;
    }
}
