using Engine.Graphics;

namespace Game.ParticleSystems;

public class WhalePlumeParticleSystem : ParticleSystem<WhalePlumeParticleSystem.Particle>
{
    private readonly float _duration;

    private readonly Random _random = new();

    private readonly float _size;

    private float _time;

    private float _toGenerate;

    public WhalePlumeParticleSystem(
        SubsystemTerrain terrain,
        float size,
        float duration
    ) : base(100)
    {
        Texture = ContentManager.Get<Texture2D>("Textures/WaterSplashParticle");
        TextureSlotsCount = 2;
        _size = size;
        _duration = duration;
    }

    public bool IsStopped { get; set; }

    public Vector3 Position { get; set; }

    public override bool Simulate(float dt)
    {
        _time += dt;
        if (_time < _duration && !IsStopped)
        {
            _toGenerate += 60f * dt;
        }
        else
        {
            _toGenerate = 0f;
        }

        var num = MathUtils.Pow(0.001f, dt);
        var num2 = MathUtils.Lerp(4f, 10f, MathUtils.Saturate(2f * _time / _duration));
        var v = new Vector3(0f, 1f, 2f);
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
                    particle.Velocity *= num;
                    particle.Velocity += v * dt;
                    particle.TextureSlot = (int)MathUtils.Min(4f * particle.Time / particle.Duration * 1.2f, 3f);
                    particle.Size = new Vector2(_size) * MathUtils.Lerp(0.1f, 0.2f, particle.Time / particle.Duration);
                }
                else
                {
                    particle.IsActive = false;
                }
            }
            else if (_toGenerate >= 1f)
            {
                particle.IsActive = true;
                var v2 = 0.1f * _size * new Vector3(_random.Float(-1f, 1f), _random.Float(0f, 2f),
                    _random.Float(-1f, 1f));
                particle.Position = Position + v2;
                particle.Color = new Color(200, 220, 210);
                particle.Velocity = 1f * _size * new Vector3(_random.Float(-1f, 1f), num2 * _random.Float(0.3f, 1f),
                    _random.Float(-1f, 1f));
                particle.Size = Vector2.Zero;
                particle.Time = 0f;
                particle.Duration = _random.Float(1f, 3f);
                particle.FlipX = _random.Bool();
                particle.FlipY = _random.Bool();
                _toGenerate -= 1f;
            }
        }

        _toGenerate = MathUtils.Remainder(_toGenerate, 1f);
        return !flag && (_time >= _duration || IsStopped);
    }

    public class Particle : Game.Particle
    {
        public float Duration;

        public float Time;

        public Vector3 Velocity;
    }
}
