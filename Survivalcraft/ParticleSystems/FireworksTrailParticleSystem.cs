using Engine.Graphics;

namespace Game.ParticleSystems;

public class FireworksTrailParticleSystem : ParticleSystem<FireworksTrailParticleSystem.Particle>, ITrailParticleSystem
{
    private Vector3? _lastPosition;

    private readonly Random _random = new();

    private float _toGenerate;

    public FireworksTrailParticleSystem() : base(60)
    {
        Texture = ContentManager.Get<Texture2D>("Textures/FireParticle");
        TextureSlotsCount = 3;
    }

    public Vector3 Position { get; set; }

    public bool IsStopped { get; set; }

    public override bool Simulate(float dt)
    {
        const float num = 120f;
        _toGenerate += num * dt;
        _lastPosition ??= Position;
        var flag = false;
        foreach (var particle in Particles)
        {
            if (particle.IsActive)
            {
                flag = true;
                particle.Time += dt;
                if (particle.Time <= particle.Duration)
                {
                    particle.TextureSlot = (int)MathUtils.Min(9f * particle.Time / particle.Duration, 8f);
                }
                else
                {
                    particle.IsActive = false;
                }
            }
            else if (!IsStopped && _toGenerate >= 1f)
            {
                particle.IsActive = true;
                particle.Position = Vector3.Lerp(_lastPosition.Value, Position, _random.Float(0f, 1f));
                particle.Color = Color.White;
                particle.Time = _random.Float(0f, 0.75f);
                particle.Size = new Vector2(_random.Float(0.12f, 0.16f));
                particle.Duration = 1f;
                particle.FlipX = _random.Bool();
                particle.FlipY = _random.Bool();
                _toGenerate -= 1f;
            }
        }

        _toGenerate = MathUtils.Remainder(_toGenerate, 1f);
        _lastPosition = Position;
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
    }
}
