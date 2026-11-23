using Engine.Graphics;

namespace Game.ParticleSystems;

public class SoundParticleSystem : ParticleSystem<SoundParticleSystem.Particle>
{
    private readonly Vector3 _direction;

    private readonly Vector3 _position;

    private readonly Random _random = new();

    public SoundParticleSystem(
        SubsystemTerrain terrain,
        Vector3 position,
        Vector3 direction
    ) : base(15)
    {
        _position = position;
        _direction = direction;
        Texture = AssetManager.Get<Texture2D>("Textures/SoundParticle");
        TextureSlotsCount = 2;
    }

    public void AddNote(Color color)
    {
        var particleIndex = 0;
        Particle particle;
        while (true)
        {
            if (particleIndex >= Particles.Length)
            {
                return;
            }

            particle = Particles[particleIndex];
            if (!Particles[particleIndex].IsActive)
            {
                break;
            }

            particleIndex++;
        }

        particle.IsActive = true;
        particle.Position = _position;
        particle.Color = Color.White;
        particle.Size = new Vector2(0.1f);
        particle.TimeToLive = _random.Float(1f, 1.5f);
        particle.Velocity = 3f * (_direction + _random.Vector3(0.5f));
        particle.BaseColor = color;
        particle.TextureSlot = _random.Int(0, TextureSlotsCount * TextureSlotsCount - 1);
        particle.BillboardingMode = ParticleBillboardingMode.Vertical;
    }

    public override bool Simulate(float dt)
    {
        dt = MathUtils.Clamp(dt, 0f, 0.1f);
        var velocityDamping = MathUtils.Pow(0.02f, dt);
        var hasActiveParticles = false;
        foreach (var particle in Particles)
        {
            if (!particle.IsActive)
            {
                continue;
            }

            hasActiveParticles = true;
            particle.TimeToLive -= dt;
            if (particle.TimeToLive > 0f)
            {
                particle.Velocity += new Vector3(0f, 5f, 0f) * dt;
                particle.Velocity *= velocityDamping;
                particle.Position += particle.Velocity * dt;
                particle.Color = particle.BaseColor * MathUtils.Saturate(2f * particle.TimeToLive);
            }
            else
            {
                particle.IsActive = false;
            }
        }

        return !hasActiveParticles;
    }

    public class Particle : Game.ParticleSystems.Particle
    {
        public Color BaseColor;

        public float TimeToLive;

        public Vector3 Velocity;
    }
}
