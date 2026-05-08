using Engine.Graphics;

namespace Game.ParticleSystems;

public class SmokeTrailParticleSystem : ParticleSystem<SmokeTrailParticleSystem.Particle>, ITrailParticleSystem
{
    private readonly Color _color;

    private float _duration;

    private readonly float _maxDuration;

    private readonly Random _random = new();

    private readonly float _size;

    private readonly float _textureSlotMultiplier;

    private readonly float _textureSlotOffset;

    private float _toGenerate;

    public SmokeTrailParticleSystem(
        int particlesCount,
        float size,
        float maxDuration,
        Color color
    ) : base(particlesCount)
    {
        _size = size;
        _maxDuration = maxDuration;
        Texture = ContentManager.Get<Texture2D>("Textures/FireParticle");
        TextureSlotsCount = 3;
        _textureSlotMultiplier = _random.Float(1.1f, 1.9f);
        _textureSlotOffset = _random.Float(0f, 1f) < 0.33f ? 3 : 0;
        _color = color;
    }

    public Vector3 Position { get; set; }

    public bool IsStopped { get; set; }

    public override bool Simulate(float dt)
    {
        _duration += dt;
        if (_duration > _maxDuration)
        {
            IsStopped = true;
        }

        var num = MathUtils.Clamp(50f / _size, 10f, 40f);
        _toGenerate += num * dt;
        var num2 = MathUtils.Pow(0.1f, dt);
        var flag = false;
        foreach (var particle in Particles)
        {
            if (particle.IsActive)
            {
                flag = true;
                particle.Time += dt;
                if (particle.Time <= particle.Duration)
                {
                    particle.Position += particle.Velocity * dt;
                    particle.Velocity *= num2;
                    particle.Velocity.Y += 10f * dt;
                    particle.TextureSlot =
                        (int)MathUtils.Min(
                            9f * particle.Time / particle.Duration * _textureSlotMultiplier + _textureSlotOffset, 8f);
                    particle.Size = new Vector2(_size * (0.15f + 0.8f * particle.Time / particle.Duration));
                }
                else
                {
                    particle.IsActive = false;
                }
            }
            else if (!IsStopped && _toGenerate >= 1f)
            {
                particle.IsActive = true;
                var v = new Vector3(_random.Float(-1f, 1f), _random.Float(-1f, 1f), _random.Float(-1f, 1f));
                particle.Position = Position + 0.025f * v;
                particle.Color = _color;
                particle.Velocity = 0.2f * v;
                particle.Time = 0f;
                particle.Size = new Vector2(0.15f * _size);
                particle.Duration = Particles.Length / num * _random.Float(0.8f, 1.05f);
                particle.FlipX = _random.Bool();
                particle.FlipY = _random.Bool();
                _toGenerate -= 1f;
            }
        }

        if (IsStopped)
        {
            return !flag;
        }

        return false;
    }

    public class Particle : Game.Particle
    {
        public float Duration;

        public float Time;

        public Vector3 Velocity;
    }
}
