using Engine.Graphics;

namespace Game.ParticleSystems;

public class OnFireParticleSystem : ParticleSystem<OnFireParticleSystem.Particle>
{
    private readonly Random _random = new();

    private float _toGenerate;

    private bool _visible;

    public OnFireParticleSystem()
        : base(25)
    {
        Texture = ContentManager.Get<Texture2D>("Textures/FireParticle");
        TextureSlotsCount = 3;
    }

    public bool IsStopped { get; set; }

    public Vector3 Position { get; set; }

    public float Radius { get; set; }

    public override bool Simulate(float dt)
    {
        var flag = false;
        if (_visible)
        {
            _toGenerate += 20f * dt;
            var num = MathUtils.Pow(0.02f, dt);
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
                        particle.Velocity.Y += 10f * dt;
                        particle.TextureSlot = (int)MathUtils.Min(9f * particle.Time / particle.Duration * 1.2f, 8f);
                    }
                    else
                    {
                        particle.IsActive = false;
                    }
                }
                else if (!IsStopped)
                {
                    if (_toGenerate >= 1f)
                    {
                        particle.IsActive = true;
                        var v = new Vector3(_random.Float(-1f, 1f), _random.Float(0f, 1f), _random.Float(-1f, 1f));
                        particle.Position = Position + 0.75f * Radius * v;
                        particle.Color = Color.White;
                        particle.Velocity = 1.5f * v;
                        particle.Size = new Vector2(0.5f);
                        particle.Time = 0f;
                        particle.Duration = _random.Float(0.5f, 1.5f);
                        particle.FlipX = _random.Bool();
                        particle.FlipY = _random.Bool();
                        _toGenerate -= 1f;
                    }
                }
                else
                {
                    _toGenerate = 0f;
                }
            }

            _toGenerate = MathUtils.Remainder(_toGenerate, 1f);
            _visible = false;
        }

        return IsStopped && !flag;
    }

    public override void Draw(Camera camera)
    {
        var num = Vector3.Dot(Position - camera.ViewPosition, camera.ViewDirection);
        if (num is <= -5f or > 48f)
        {
            return;
        }

        _visible = true;
        base.Draw(camera);
    }

    public class Particle : Game.Particle
    {
        public float Duration;

        public float Time;

        public Vector3 Velocity;
    }
}
