using Engine.Graphics;

namespace Game.ParticleSystems;

public class GunSmokeParticleSystem : ParticleSystem<GunSmokeParticleSystem.Particle>
{
    private readonly Color _color;

    private readonly Vector3 _direction;

    private readonly Vector3 _position;

    private readonly Random _random = new();

    private float _time;

    private float _toGenerate;

    public GunSmokeParticleSystem(SubsystemTerrain terrain, Vector3 position, Vector3 direction)
        : base(50)
    {
        Texture = ContentManager.Get<Texture2D>("Textures/GunSmokeParticle");
        TextureSlotsCount = 3;
        _position = position;
        _direction = Vector3.Normalize(direction);
        var num = Terrain.ToCell(position.X);
        var num2 = Terrain.ToCell(position.Y);
        var num3 = Terrain.ToCell(position.Z);
        var x = 0;
        x = MathUtils.Max(x, terrain.Terrain.GetCellLight(num + 1, num2, num3));
        x = MathUtils.Max(x, terrain.Terrain.GetCellLight(num - 1, num2, num3));
        x = MathUtils.Max(x, terrain.Terrain.GetCellLight(num, num2 + 1, num3));
        x = MathUtils.Max(x, terrain.Terrain.GetCellLight(num, num2 - 1, num3));
        x = MathUtils.Max(x, terrain.Terrain.GetCellLight(num, num2, num3 + 1));
        x = MathUtils.Max(x, terrain.Terrain.GetCellLight(num, num2, num3 - 1));
        var num4 = LightingManager.LightIntensityByLightValue[x];
        _color = new Color(num4, num4, num4);
    }

    public override bool Simulate(float dt)
    {
        _time += dt;
        var num = MathUtils.Lerp(150f, 20f, MathUtils.Saturate(2f * _time / 0.5f));
        var num2 = MathUtils.Pow(0.01f, dt);
        var s = MathUtils.Lerp(20f, 0f, MathUtils.Saturate(2f * _time / 0.5f));
        var v = new Vector3(2f, 2f, 1f);
        if (_time < 0.5f)
        {
            _toGenerate += num * dt;
        }
        else
        {
            _toGenerate = 0f;
        }

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
                    particle.Velocity += v * dt;
                    particle.TextureSlot = (int)MathUtils.Min(9f * particle.Time / particle.Duration, 8f);
                    particle.Size = new Vector2(0.3f);
                }
                else
                {
                    particle.IsActive = false;
                }
            }
            else if (_toGenerate >= 1f)
            {
                particle.IsActive = true;
                var v2 = _random.Vector3(0f, 1f);
                particle.Position = _position + 0.3f * v2;
                particle.Color = _color;
                particle.Velocity = s * (_direction + _random.Vector3(0f, 0.1f)) + 2.5f * v2;
                particle.Size = Vector2.Zero;
                particle.Time = 0f;
                particle.Duration = _random.Float(0.5f, 2f);
                particle.FlipX = _random.Bool();
                particle.FlipY = _random.Bool();
                _toGenerate -= 1f;
            }
        }

        _toGenerate = MathUtils.Remainder(_toGenerate, 1f);
        return !flag && _time >= 0.5f;
    }

    public class Particle : Game.Particle
    {
        public float Duration;

        public float Time;

        public Vector3 Velocity;
    }
}
