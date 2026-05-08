using Engine.Graphics;

namespace Game.ParticleSystems;

public class FireParticleSystem : ParticleSystem<FireParticleSystem.Particle>
{
    private float _age;

    private readonly float _maxVisibilityDistance;

    private readonly Vector3 _position;

    private readonly Random _random = new();

    private readonly float _size;

    private float _toGenerate;

    private bool _visible;

    public FireParticleSystem(
        Vector3 position,
        float size,
        float maxVisibilityDistance
    ) : base(10)
    {
        _position = position;
        _size = size;
        _maxVisibilityDistance = maxVisibilityDistance;
        Texture = ContentManager.Get<Texture2D>("Textures/FireParticle");
        TextureSlotsCount = 3;
    }

    public bool IsStopped { get; set; }

    public override bool Simulate(float dt)
    {
        _age += dt;
        var flag = false;
        if (_visible || _age < 2f)
        {
            _toGenerate += IsStopped ? 0f : 5f * dt;
            foreach (var particle in Particles)
            {
                if (particle.IsActive)
                {
                    flag = true;
                    particle.Time += dt;
                    particle.TimeToLive -= dt;
                    if (particle.TimeToLive > 0f)
                    {
                        particle.Position.Y += particle.Speed * dt;
                        particle.TextureSlot = (int)MathUtils.Min(9f * particle.Time / 1.25f, 8f);
                    }
                    else
                    {
                        particle.IsActive = false;
                    }
                }
                else if (_toGenerate >= 1f)
                {
                    particle.IsActive = true;
                    particle.Position = _position +
                                        0.25f * _size * new Vector3(_random.Float(-1f, 1f), 0f,
                                            _random.Float(-1f, 1f));
                    particle.Color = Color.White;
                    particle.Size = new Vector2(_size);
                    particle.Speed = _random.Float(0.45f, 0.55f) * _size / 0.15f;
                    particle.Time = 0f;
                    particle.TimeToLive = _random.Float(0.5f, 2f);
                    particle.FlipX = _random.Int(0, 1) == 0;
                    particle.FlipY = _random.Int(0, 1) == 0;
                    _toGenerate -= 1f;
                }
            }

            _toGenerate = MathUtils.Remainder(_toGenerate, 1f);
        }

        _visible = false;
        if (IsStopped)
        {
            return !flag;
        }

        return false;
    }

    public override void Draw(Camera camera)
    {
        var num = Vector3.Dot(_position - camera.ViewPosition, camera.ViewDirection);
        if (!(num > -0.5f) ||
            !(num <= _maxVisibilityDistance) ||
            !(Vector3.DistanceSquared(_position, camera.ViewPosition) <=
              _maxVisibilityDistance * _maxVisibilityDistance))
        {
            return;
        }

        _visible = true;
        base.Draw(camera);
    }

    public class Particle : Game.Particle
    {
        public float Speed;

        public float Time;

        public float TimeToLive;
    }
}
