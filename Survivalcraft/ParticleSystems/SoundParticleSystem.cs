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
        Texture = ContentManager.Get<Texture2D>("Textures/SoundParticle");
        TextureSlotsCount = 2;
    }

    public void AddNote(Color color)
    {
        var num = 0;
        Particle particle;
        while (true)
        {
            if (num >= Particles.Length)
            {
                return;
            }

            particle = Particles[num];
            if (!Particles[num].IsActive)
            {
                break;
            }

            num++;
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
        var num = MathUtils.Pow(0.02f, dt);
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
                particle.Velocity += new Vector3(0f, 5f, 0f) * dt;
                particle.Velocity *= num;
                particle.Position += particle.Velocity * dt;
                particle.Color = particle.BaseColor * MathUtils.Saturate(2f * particle.TimeToLive);
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
        public Color BaseColor;

        public float TimeToLive;

        public Vector3 Velocity;
    }
}
