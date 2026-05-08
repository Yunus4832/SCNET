using Engine.Graphics;

namespace Game.ParticleSystems;

public class PukeParticleSystem : ParticleSystem<PukeParticleSystem.Particle>
{
    private float _duration;

    private readonly Random _random = new();

    private readonly SubsystemTerrain _subsystemTerrain;

    private float _toGenerate;

    public PukeParticleSystem(SubsystemTerrain terrain) : base(80)
    {
        _subsystemTerrain = terrain;
        Texture = ContentManager.Get<Texture2D>("Textures/PukeParticle");
        TextureSlotsCount = 3;
    }

    public Vector3 Position { get; set; }

    public Vector3 Direction { get; set; }

    public bool IsStopped { get; set; }

    public override bool Simulate(float dt)
    {
        var num = Terrain.ToCell(Position.X);
        var num2 = Terrain.ToCell(Position.Y);
        var num3 = Terrain.ToCell(Position.Z);
        var x = 0;
        x = MathUtils.Max(x, _subsystemTerrain.Terrain.GetCellLight(num + 1, num2, num3));
        x = MathUtils.Max(x, _subsystemTerrain.Terrain.GetCellLight(num - 1, num2, num3));
        x = MathUtils.Max(x, _subsystemTerrain.Terrain.GetCellLight(num, num2 + 1, num3));
        x = MathUtils.Max(x, _subsystemTerrain.Terrain.GetCellLight(num, num2 - 1, num3));
        x = MathUtils.Max(x, _subsystemTerrain.Terrain.GetCellLight(num, num2, num3 + 1));
        x = MathUtils.Max(x, _subsystemTerrain.Terrain.GetCellLight(num, num2, num3 - 1));
        var white = Color.White;
        var num4 = LightingManager.LightIntensityByLightValue[x];
        white *= num4;
        white.A = 255;
        dt = MathUtils.Clamp(dt, 0f, 0.1f);
        var num5 = MathUtils.Pow(0.03f, dt);
        _duration += dt;
        if (_duration > 3.5f)
        {
            IsStopped = true;
        }

        var num6 = MathUtils.Saturate(1.3f * SimplexNoise.Noise(3f * _duration + GetHashCode() % 100) - 0.3f);
        var num7 = 30f * num6;
        _toGenerate += num7 * dt;
        var flag = false;
        foreach (var particle in Particles)
        {
            if (particle.IsActive)
            {
                flag = true;
                particle.TimeToLive -= dt;
                if (particle.TimeToLive > 0f)
                {
                    var position = particle.Position;
                    var vector = position + particle.Velocity * dt;
                    var terrainRaycastResult = _subsystemTerrain.Raycast(position, vector, false, true,
                        (value, _) => BlocksManager.Blocks[Terrain.ExtractContents(value)].Collidable);
                    if (terrainRaycastResult.HasValue)
                    {
                        var plane = terrainRaycastResult.Value.CellFace.CalculatePlane();
                        vector = position;
                        if (plane.Normal.X != 0f)
                        {
                            particle.Velocity *= new Vector3(-0.05f, 0.05f, 0.05f);
                        }

                        if (plane.Normal.Y != 0f)
                        {
                            particle.Velocity *= new Vector3(0.05f, -0.05f, 0.05f);
                        }

                        if (plane.Normal.Z != 0f)
                        {
                            particle.Velocity *= new Vector3(0.05f, 0.05f, -0.05f);
                        }
                    }

                    particle.Position = vector;
                    particle.Velocity.Y += -9.81f * dt;
                    particle.Velocity *= num5;
                    particle.Color *= MathUtils.Saturate(particle.TimeToLive);
                    particle.TextureSlot = (int)(8.99f * MathUtils.Saturate(3f - particle.TimeToLive));
                }
                else
                {
                    particle.IsActive = false;
                }
            }
            else if (!IsStopped && _toGenerate >= 1f)
            {
                var v = _random.Vector3(0f, 1f);
                particle.IsActive = true;
                particle.Position = Position + 0.05f * v;
                particle.Color = Color.MultiplyColorOnly(white, _random.Float(0.7f, 1f));
                particle.Velocity = MathUtils.Lerp(1f, 2.5f, num6) * Vector3.Normalize(Direction + 0.25f * v);
                particle.TimeToLive = 3f;
                particle.Size = new Vector2(0.1f);
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
        public float TimeToLive;

        public Vector3 Velocity;
    }
}
