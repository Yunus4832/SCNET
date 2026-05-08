using Engine.Graphics;

namespace Game.ParticleSystems;

public class RainSplashParticleSystem : ParticleSystem<RainSplashParticleSystem.Particle>
{
    private bool _isActive;

    private readonly Random _random = new();

    public RainSplashParticleSystem() : base(150)
    {
        Texture = ContentManager.Get<Texture2D>("Textures/RainSplashParticle");
        TextureSlotsCount = 1;
    }

    public void AddSplash(int value, Vector3 position, Color color)
    {
        foreach (var particle in Particles)
        {
            if (particle.IsActive)
            {
                continue;
            }

            var block = BlocksManager.Blocks[Terrain.ExtractContents(value)];
            particle.IsActive = true;
            particle.Position = position;
            particle.BaseColor = color;
            if (block is WaterBlock)
            {
                particle.Position.Y += 0.05f;
                particle.BaseSize1 = 0.02f;
                particle.BaseSize2 = 0.09f;
                particle.Duration = particle.TimeToLive = _random.Float(0.3f, 0.5f);
                particle.Velocity = Vector3.Zero;
                particle.Gravity = 0f;
                particle.BillboardingMode = ParticleBillboardingMode.Horizontal;
                particle.FadeFactor = 1.6f;
            }
            else if (block.Collidable)
            {
                particle.BaseSize1 = 0.03f;
                particle.BaseSize2 = 0.08f;
                particle.Duration = particle.TimeToLive = _random.Float(0.25f, 0.3f);
                particle.Velocity = _random.Float(0.7f, 0.9f) * Vector3.UnitY;
                particle.Gravity = -10f;
                particle.BillboardingMode = ParticleBillboardingMode.Camera;
                particle.FadeFactor = 2.8f;
            }
            else if (_random.Bool(0.33f))
            {
                particle.BaseSize1 = _random.Float(0.015f, 0.025f);
                particle.BaseSize2 = particle.BaseSize1;
                particle.Duration = particle.TimeToLive = _random.Float(0.25f, 0.3f);
                particle.Velocity = _random.Vector3(0f, 1.5f) * new Vector3(1f, 0f, 1f);
                particle.Gravity = -10f;
                particle.BillboardingMode = ParticleBillboardingMode.Camera;
                particle.FadeFactor = 2.8f;
            }

            break;
        }

        _isActive = true;
    }

    public override bool Simulate(float dt)
    {
        if (_isActive)
        {
            dt = MathUtils.Clamp(dt, 0f, 0.1f);
            var num = MathUtils.Pow(0.0005f, dt);
            var flag = false;
            for (var i = 0; i < Particles.Length; i++)
            {
                var particle = Particles[i];
                if (particle.IsActive)
                {
                    particle.Position += particle.Velocity * dt;
                    particle.Velocity.Y += particle.Gravity * dt;
                    particle.Velocity *= num;
                    particle.Size = new Vector2(MathUtils.Lerp(particle.BaseSize1, particle.BaseSize2,
                        (particle.Duration - particle.TimeToLive) / particle.Duration));
                    particle.Color = particle.BaseColor * MathUtils.Saturate(particle.FadeFactor * particle.TimeToLive);
                    particle.TimeToLive -= dt;
                    particle.FlipX = _random.Bool();
                    particle.FlipY = _random.Bool();
                    if (particle.TimeToLive <= 0f)
                    {
                        particle.IsActive = false;
                    }
                    else
                    {
                        flag = true;
                    }
                }
            }

            if (!flag)
            {
                _isActive = false;
            }
        }

        return false;
    }

    public class Particle : Game.Particle
    {
        public Color BaseColor;

        public float BaseSize1;

        public float BaseSize2;

        public float Duration;

        public float FadeFactor;

        public float Gravity;

        public float TimeToLive;

        public Vector3 Velocity;
    }
}
