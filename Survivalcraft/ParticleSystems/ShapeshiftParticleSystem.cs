using Engine.Graphics;

namespace Game.ParticleSystems;

public class ShapeshiftParticleSystem : ParticleSystem<ShapeshiftParticleSystem.Particle>
{
    private float _generationSpeed;

    private readonly Random _random = new();

    private float _toGenerate;

    public ShapeshiftParticleSystem() : base(40)
    {
        Texture = ContentManager.Get<Texture2D>("Textures/ShapeshiftParticle");
        TextureSlotsCount = 3;
    }

    public bool Stopped { get; set; }

    public Vector3 Position { get; set; }

    public BoundingBox BoundingBox { get; set; }

    public override bool Simulate(float dt)
    {
        var flag = false;
        _generationSpeed = MathUtils.Min(_generationSpeed + 15f * dt, 35f);
        _toGenerate += _generationSpeed * dt;
        foreach (var particle in Particles)
        {
            if (particle.IsActive)
            {
                flag = true;
                particle.Time += dt;
                if (particle.Time <= particle.Duration)
                {
                    particle.Position += particle.Velocity * dt;
                    particle.FlipX = _random.Bool();
                    particle.FlipY = _random.Bool();
                    particle.TextureSlot = (int)MathUtils.Min(9.900001f * particle.Time / particle.Duration, 8f);
                }
                else
                {
                    particle.IsActive = false;
                }
            }
            else if (!Stopped)
            {
                while (_toGenerate >= 1f)
                {
                    particle.IsActive = true;
                    particle.Position.X = _random.Float(BoundingBox.Min.X, BoundingBox.Max.X);
                    particle.Position.Y = _random.Float(BoundingBox.Min.Y, BoundingBox.Max.Y);
                    particle.Position.Z = _random.Float(BoundingBox.Min.Z, BoundingBox.Max.Z);
                    particle.Velocity = new Vector3(0f, _random.Float(0.5f, 1.5f), 0f);
                    particle.Color = Color.White;
                    particle.Size = new Vector2(0.4f);
                    particle.Time = 0f;
                    particle.Duration = _random.Float(0.75f, 1.5f);
                    _toGenerate -= 1f;
                }
            }
            else
            {
                _toGenerate = 0f;
            }
        }

        _toGenerate = MathUtils.Remainder(_toGenerate, 1f);
        return Stopped && !flag;
    }

    public class Particle : Game.Particle
    {
        public float Duration;

        public float Time;

        public Vector3 Velocity;
    }
}
