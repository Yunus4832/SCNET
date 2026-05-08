namespace Game.ParticleSystems;

public class BlockDebrisParticleSystem : ParticleSystem<BlockDebrisParticleSystem.Particle>
{
    private readonly Random _random = new();

    private readonly SubsystemTerrain _subsystemTerrain;

    public BlockDebrisParticleSystem(
        SubsystemTerrain terrain,
        Vector3 position,
        float strength,
        float scale,
        Color color,
        int textureSlot
    ) : base((int)(50f * strength))
    {
        _subsystemTerrain = terrain;
        Texture = terrain.Project.FindSubsystem<SubsystemBlocksTexture>(true)!.BlocksTexture;
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
        TextureSlotsCount = 32;
        var num4 = LightingManager.LightIntensityByLightValue[x];
        color *= num4;
        color.A = 255;
        var num5 = MathUtils.Sqrt(strength);
        for (var i = 0; i < Particles.Length; i++)
        {
            var obj = Particles[i];
            obj.IsActive = true;
            var vector = new Vector3(_random.Float(-1f, 1f), _random.Float(-1f, 1f), _random.Float(-1f, 1f));
            obj.Position = position + strength * 0.45f * vector;
            obj.Color = Color.MultiplyColorOnly(color, _random.Float(0.7f, 1f));
            obj.Size = num5 * scale * new Vector2(_random.Float(0.05f, 0.06f));
            obj.TimeToLive = num5 * _random.Float(1f, 3f);
            obj.Velocity = num5 * 2f *
                           (vector + new Vector3(_random.Float(-0.2f, 0.2f), 0.6f, _random.Float(-0.2f, 0.2f)));
            obj.TextureSlot = textureSlot % 16 * 2 + _random.Int(0, 1) +
                              32 * (textureSlot / 16 * 2 + _random.Int(0, 1));
        }
    }

    public override bool Simulate(float dt)
    {
        dt = MathUtils.Clamp(dt, 0f, 0.1f);
        var num = MathUtils.Pow(0.1f, dt);
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
                var terrainRaycastResult = _subsystemTerrain.Raycast(position, vector, false, true,
                    (value, _) => BlocksManager.Blocks[Terrain.ExtractContents(value)].Collidable);
                if (terrainRaycastResult.HasValue)
                {
                    var plane = terrainRaycastResult.Value.CellFace.CalculatePlane();
                    vector = position;
                    if (plane.Normal.X != 0f)
                    {
                        particle.Velocity *= new Vector3(-0.25f, 0.25f, 0.25f);
                    }

                    if (plane.Normal.Y != 0f)
                    {
                        particle.Velocity *= new Vector3(0.25f, -0.25f, 0.25f);
                    }

                    if (plane.Normal.Z != 0f)
                    {
                        particle.Velocity *= new Vector3(0.25f, 0.25f, -0.25f);
                    }
                }

                particle.Position = vector;
                particle.Velocity.Y += -9.81f * dt;
                particle.Velocity *= num;
                particle.Color *= MathUtils.Saturate(particle.TimeToLive);
            }
            else
            {
                particle.IsActive = false;
            }
        }

        return !flag;
    }

    public class Particle : Game.Particle
    {
        public float TimeToLive;
        public Vector3 Velocity;
    }
}
