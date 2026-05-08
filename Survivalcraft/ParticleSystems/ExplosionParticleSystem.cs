using Engine.Graphics;

namespace Game.ParticleSystems;

public class ExplosionParticleSystem : ParticleSystem<ExplosionParticleSystem.Particle>
{
    private const float _duration = 1.5f;

    private readonly List<Particle> _inactiveParticles = [];

    private bool _isEmpty;

    private readonly Dictionary<Point3, Particle> _particlesByPoint = new();

    private readonly Random _random = new();

    public ExplosionParticleSystem() : base(1000)
    {
        Texture = ContentManager.Get<Texture2D>("Textures/FireParticle");
        TextureSlotsCount = 3;
        _inactiveParticles.AddRange(Particles);
    }

    public void SetExplosionCell(Point3 point, float strength)
    {
        if (!_particlesByPoint.TryGetValue(point, out var value))
        {
            if (_inactiveParticles.Count > 0)
            {
                value = _inactiveParticles[^1];
                _inactiveParticles.RemoveAt(_inactiveParticles.Count - 1);
            }
            else
            {
                for (var i = 0; i < 5; i++)
                {
                    var num = _random.Int(0, Particles.Length - 1);
                    if (strength > Particles[num].Strength)
                    {
                        value = Particles[num];
                    }
                }
            }

            if (value != null)
            {
                _particlesByPoint.Add(point, value);
            }
        }

        if (value == null)
        {
            return;
        }

        value.IsActive = true;
        value.Position = new Vector3(point.X, point.Y, point.Z) + new Vector3(0.5f) + 0.2f *
            new Vector3(_random.Float(-1f, 1f), _random.Float(-1f, 1f), _random.Float(-1f, 1f));
        value.Size = new Vector2(_random.Float(0.6f, 0.9f));
        value.Strength = strength;
        value.Color = Color.White;
        _isEmpty = false;
    }

    public override bool Simulate(float dt)
    {
        if (_isEmpty)
        {
            return false;
        }

        _isEmpty = true;
        foreach (var particle in Particles)
        {
            if (!particle.IsActive)
            {
                continue;
            }

            _isEmpty = false;
            particle.Strength -= dt / 1.5f;
            if (particle.Strength > 0f)
            {
                particle.TextureSlot = (int)MathUtils.Min(9f * (1f - particle.Strength) * 0.6f, 8f);
                particle.Position.Y += 2f * MathUtils.Max(1f - particle.Strength - 0.25f, 0f) * dt;
            }
            else
            {
                particle.IsActive = false;
                _inactiveParticles.Add(particle);
            }
        }

        return false;
    }

    public override void Draw(Camera camera)
    {
        if (!_isEmpty)
        {
            base.Draw(camera);
        }
    }

    public class Particle : Game.Particle
    {
        public float Strength;
    }
}
