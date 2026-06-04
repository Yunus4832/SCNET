namespace Game.ParticleSystems;

public class PaintParticleSystem : ParticleSystem<PaintParticleSystem.Particle>
{
    private readonly Color _color;

    private readonly Random _random = new();

    private readonly SubsystemTerrain _subsystemTerrain = null!;

    public PaintParticleSystem(SubsystemTerrain terrain, Vector3 position, Vector3 normal, Color color)
        : base(20)
    {
        if (RunMode.Value is RunModeType.HeadlessServer)
        {
            return;
        }

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
        TextureSlotsCount = 16;
        var s = LightingManager.LightIntensityByLightValue[x];
        _color = color * s;
        _color.A = color.A;
        var vector = Vector3.Normalize(Vector3.Cross(normal, new Vector3(0.37f, 0.15f, 0.17f)));
        var v = Vector3.Normalize(Vector3.Cross(normal, vector));
        foreach (var particle in Particles)
        {
            particle.IsActive = true;
            var vector2 = new Vector2(_random.Float(-1f, 1f), _random.Float(-1f, 1f));
            particle.Position = position + 0.4f * (vector2.X * vector + vector2.Y * v) + 0.03f * normal;
            particle.Color = _color;
            particle.Size = new Vector2(_random.Float(0.025f, 0.035f));
            particle.TimeToLive = _random.Float(0.5f, 1.5f);
            particle.Velocity = 1f * (vector2.X * vector + vector2.Y * v) + _random.Float(-3f, 0.5f) * normal;
            particle.TextureSlot = 15;
            particle.Alpha = _random.Float(0.3f, 0.6f);
        }
    }

    public override bool Simulate(float dt)
    {
        dt = MathUtils.Clamp(dt, 0f, 0.1f);
        var num = MathUtils.Pow(0.2f, dt);
        var num2 = MathUtils.Pow(1E-07f, dt);
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
                    particle.Velocity = Vector3.Zero;
                    particle.Position = terrainRaycastResult.Value.HitPoint(0.03f);
                    particle.HighDampingFactor = _random.Float(0.5f, 1f);
                    if (terrainRaycastResult.Value.CellFace.Face >= 4)
                    {
                        particle.NoGravity = true;
                    }
                }
                else
                {
                    particle.Position = vector;
                }

                if (!particle.NoGravity)
                {
                    particle.Velocity.Y += -9.81f * dt;
                }

                particle.Velocity *= particle.HighDampingFactor > 0f ? num2 * particle.HighDampingFactor : num;
                particle.Color = _color * MathUtils.Saturate(1.5f * particle.TimeToLive * particle.Alpha);
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
        public float Alpha;

        public float HighDampingFactor;

        public bool NoGravity;

        public float TimeToLive;

        public Vector3 Velocity;
    }
}
