using Engine.Graphics;

namespace Game.ParticleSystems;

public class BurntDebrisParticleSystem : ParticleSystem<BurntDebrisParticleSystem.Particle>
{
    private readonly Random _random = new();

    private readonly SubsystemTerrain _subsystemTerrain;

    public BurntDebrisParticleSystem(
        SubsystemTerrain terrain,
        int x,
        int y,
        int z
    ) : this(terrain, new Vector3(x + 0.5f, y + 0.5f, z + 0.5f))
    {
    }

    public BurntDebrisParticleSystem(SubsystemTerrain terrain, Vector3 position)
        : base(15)
    {
        _subsystemTerrain = terrain;
        Texture = ContentManager.Get<Texture2D>("Textures/FireParticle");
        TextureSlotsCount = 3;
        SetLightOrigin(terrain, position);
        var white = Color.White;
        white.A = 255;
        foreach (var particle in Particles)
        {
            particle.IsActive = true;
            particle.Position = position +
                                0.5f *
                                new Vector3(
                                    _random.Float(-1f, 1f),
                                    _random.Float(-1f, 1f),
                                    _random.Float(-1f, 1f)
                                );
            particle.Color = white;
            particle.Size = new Vector2(0.5f);
            particle.TimeToLive = _random.Float(0.75f, 2f);
            particle.Velocity = new Vector3(
                3f * _random.Float(-1f, 1f),
                2f * _random.Float(-1f, 1f),
                3f * _random.Float(-1f, 1f)
            );
            particle.TextureSlot = 8;
        }
    }

    public override bool Simulate(float dt)
    {
        dt = MathUtils.Clamp(dt, 0f, 0.1f);
        var num = MathUtils.Pow(0.04f, dt);
        var flag = false;
        foreach (var particle in Particles)
        {
            if (!particle.IsActive)
            {
                continue;
            }

            flag = true;
            particle.TimeToLive -= dt;
            if (particle.TimeToLive > 0f)
            {
                var position = particle.Position;
                var vector = position + particle.Velocity * dt;
                var terrainRaycastResult = _subsystemTerrain.Raycast(
                    position,
                    vector,
                    false,
                    true,
                    (value, _) => _subsystemTerrain.Catalog.Blocks[Terrain.ExtractContents(value)]
                        .Collidable
                );
                if (terrainRaycastResult.HasValue)
                {
                    var plane = terrainRaycastResult.Value.CellFace.CalculatePlane();
                    vector = position;
                    if (plane.Normal.X != 0f)
                    {
                        particle.Velocity *= new Vector3(-0.1f, 0.1f, 0.1f);
                    }

                    if (plane.Normal.Y != 0f)
                    {
                        particle.Velocity *= new Vector3(0.1f, -0.1f, 0.1f);
                    }

                    if (plane.Normal.Z != 0f)
                    {
                        particle.Velocity *= new Vector3(0.1f, 0.1f, -0.1f);
                    }
                }

                particle.Position = vector;
                particle.Velocity.Y += -10f * dt;
                particle.Velocity *= num;
                particle.Color *= MathUtils.Saturate(particle.TimeToLive);
                particle.TextureSlot = (int)(8.99f * MathUtils.Saturate(2f - particle.TimeToLive));
            }
            else
            {
                particle.IsActive = false;
            }
        }

        return !flag;
    }

    public class Particle : Game.ParticleSystems.Particle
    {
        public float TimeToLive;

        public Vector3 Velocity;
    }
}
