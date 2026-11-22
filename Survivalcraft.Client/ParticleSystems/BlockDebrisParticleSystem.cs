namespace Game.ParticleSystems;

public class BlockDebrisParticleSystem : ParticleSystem<BlockDebrisParticleSystem.Particle>
{
    private readonly Random _random = new();

    private readonly SubsystemTerrain _subsystemTerrain = null!;

    public BlockDebrisParticleSystem(
        SubsystemTerrain terrain,
        Vector3 position,
        float strength,
        float scale,
        Color color,
        int textureSlot
    ) : base((int)(50f * strength))
    {
        if (!WorldRuntimeContext.For(terrain.Project).HasPresentation)
        {
            return;
        }

        _subsystemTerrain = terrain;
        Texture = terrain.Project.FindSubsystem<SubsystemBlocksTexture>(true)!.BlocksTexture;
        SetLightOrigin(terrain, position);
        TextureSlotsCount = 32;
        color.A = 255;
        var strengthScale = MathUtils.Sqrt(strength);
        foreach (var obj in Particles)
        {
            obj.IsActive = true;
            var vector = new Vector3(_random.Float(-1f, 1f), _random.Float(-1f, 1f), _random.Float(-1f, 1f));
            obj.Position = position + strength * 0.45f * vector;
            obj.Color = Color.MultiplyColorOnly(color, _random.Float(0.7f, 1f));
            obj.Size = strengthScale * scale * new Vector2(_random.Float(0.05f, 0.06f));
            obj.TimeToLive = strengthScale * _random.Float(1f, 3f);
            obj.Velocity = strengthScale *
                           2f *
                           (vector + new Vector3(_random.Float(-0.2f, 0.2f), 0.6f, _random.Float(-0.2f, 0.2f)));
            obj.TextureSlot = textureSlot % 16 * 2 +
                              _random.Int(0, 1) +
                              32 * (textureSlot / 16 * 2 + _random.Int(0, 1));
        }
    }

    public override bool Simulate(float dt)
    {
        dt = MathUtils.Clamp(dt, 0f, 0.1f);
        var velocityDamping = MathUtils.Pow(0.1f, dt);
        var hasActiveParticles = false;
        foreach (var particle in Particles)
        {
            if (!particle.IsActive)
            {
                continue;
            }

            hasActiveParticles = true;
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
                particle.Velocity *= velocityDamping;
                particle.Color *= MathUtils.Saturate(particle.TimeToLive);
            }
            else
            {
                particle.IsActive = false;
            }
        }

        return !hasActiveParticles;
    }

    public class Particle : Game.ParticleSystems.Particle
    {
        public float TimeToLive;

        public Vector3 Velocity;
    }
}
