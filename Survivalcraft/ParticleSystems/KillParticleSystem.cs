using Engine.Graphics;

namespace Game.ParticleSystems;

public class KillParticleSystem : ParticleSystem<KillParticleSystem.Particle>
{
    private readonly Random _random = new();

    public KillParticleSystem(
        SubsystemTerrain terrain,
        Vector3 position,
        float size
    ) : base(20)
    {
        Texture = ContentManager.Get<Texture2D>("Textures/KillParticle");
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
        TextureSlotsCount = 2;
        var white = Color.White;
        var num4 = LightingManager.LightIntensityByLightValue[x];
        white *= num4;
        white.A = 255;
        foreach (var particle in Particles)
        {
            particle.IsActive = true;
            particle.Position = position + 0.4f * size *
                new Vector3(_random.Float(-1f, 1f), _random.Float(-1f, 1f), _random.Float(-1f, 1f));
            particle.Color = white;
            particle.Size = new Vector2(0.3f * size);
            particle.TimeToLive = _random.Float(0.5f, 3.5f);
            particle.Velocity = 1.2f * size *
                                new Vector3(_random.Float(-1f, 1f), _random.Float(-1f, 1f), _random.Float(-1f, 1f));
            particle.FlipX = _random.Bool();
            particle.FlipY = _random.Bool();
        }
    }

    public override bool Simulate(float dt)
    {
        dt = MathUtils.Clamp(dt, 0f, 0.1f);
        var num = MathUtils.Pow(0.1f, dt);
        var flag = false;
        foreach (var particle in Particles)
        {
            if (!particle.IsActive)
            {
                continue;
            }

            flag = true;
            particle.TimeToLive -= dt;
            if (particle.TimeToLive > 0f)
            {
                _ = particle.Position += particle.Velocity * dt;
                particle.Velocity.Y += 1f * dt;
                particle.Velocity *= num;
                particle.TextureSlot = (int)(3.99f * MathUtils.Saturate(2f - particle.TimeToLive));
            }
            else
            {
                particle.IsActive = false;
            }
        }

        return !flag;
    }

    public class Particle : Game.Particle
    {
        public float TimeToLive;
        public Vector3 Velocity;
    }
}
