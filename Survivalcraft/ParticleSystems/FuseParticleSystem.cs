using Engine.Graphics;

namespace Game.ParticleSystems;

public class FuseParticleSystem : ParticleSystem<FuseParticleSystem.Particle>
{
    private readonly Vector3 _position;

    private readonly Random _random = new();

    private float _toGenerate;

    private bool _visible;

    public FuseParticleSystem(Vector3 position)
        : base(15)
    {
        _position = position;
        Texture = ContentManager.Get<Texture2D>("Textures/FireParticle");
        TextureSlotsCount = 3;
    }

    public override bool Simulate(float dt)
    {
        if (_visible)
        {
            _toGenerate += 15f * dt;
            foreach (var particle in Particles)
            {
                if (particle.IsActive)
                {
                    particle.Time += dt;
                    particle.TimeToLive -= dt;
                    if (particle.TimeToLive > 0f)
                    {
                        particle.Position.Y += particle.Speed * dt;
                        particle.Speed = MathUtils.Max(particle.Speed - 1.5f * dt, particle.TargetSpeed);
                        particle.TextureSlot = (int)MathUtils.Min(9f * particle.Time / 0.75f, 8f);
                        particle.Size = new Vector2(0.07f * (1f + 2f * particle.Time));
                    }
                    else
                    {
                        particle.IsActive = false;
                    }
                }
                else if (_toGenerate >= 1f)
                {
                    particle.IsActive = true;
                    particle.Position = _position + 0.02f * new Vector3(0f, _random.Float(-1f, 1f), 0f);
                    particle.Color = Color.White;
                    particle.TargetSpeed = _random.Float(0.45f, 0.55f) * 0.4f;
                    particle.Speed = _random.Float(0.45f, 0.55f) * 2.5f;
                    particle.Time = 0f;
                    particle.Size = Vector2.Zero;
                    particle.TimeToLive = _random.Float(0.3f, 1f);
                    particle.FlipX = _random.Int(0, 1) == 0;
                    particle.FlipY = _random.Int(0, 1) == 0;
                    _toGenerate -= 1f;
                }
            }

            _toGenerate = MathUtils.Remainder(_toGenerate, 1f);
        }

        _visible = false;
        return false;
    }

    public override void Draw(Camera camera)
    {
        var num = Vector3.Dot(_position - camera.ViewPosition, camera.ViewDirection);
        if (num is <= -0.5f or > 32f || !(Vector3.DistanceSquared(_position, camera.ViewPosition) <= 1024f))
        {
            return;
        }

        _visible = true;
        base.Draw(camera);
    }

    public class Particle : Game.Particle
    {
        public float Speed;

        public float TargetSpeed;

        public float Time;

        public float TimeToLive;
    }
}
