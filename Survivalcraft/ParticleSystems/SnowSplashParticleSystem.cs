using Engine.Graphics;

namespace Game.ParticleSystems;

public class SnowSplashParticleSystem : ParticleSystem<SnowSplashParticleSystem.Particle>
{
    private bool _isActive;

    private readonly Random _random = new();

    public SnowSplashParticleSystem() : base(100)
    {
        Texture = ContentManager.Get<Texture2D>("Textures/SnowParticle");
        TextureSlotsCount = 4;
    }

    public void AddSplash(int value, Vector3 position, Vector2 size, Color color, int textureSlot)
    {
        foreach (var particle in Particles)
        {
            if (particle.IsActive)
            {
                continue;
            }

            var block = BlocksManager.Blocks[Terrain.ExtractContents(value)];
            particle.IsActive = true;
            particle.Position = position;
            particle.BaseColor = color;
            particle.BillboardingMode = ParticleBillboardingMode.Horizontal;
            particle.Size = size;
            particle.TextureSlot = textureSlot;
            if (block is WaterBlock waterBlock)
            {
                waterBlock.GetLevelHeight(FluidBlock.GetLevel(Terrain.ExtractData(value)));
                particle.TimeToLive = _random.Float(0.2f, 0.3f);
                particle.FadeFactor = 1f;
            }
            else if (block.Collidable || block is SnowBlock)
            {
                particle.TimeToLive = _random.Float(0.8f, 1.2f);
                particle.FadeFactor = 1f;
            }

            break;
        }

        _isActive = true;
    }

    public override bool Simulate(float dt)
    {
        if (!_isActive)
        {
            return false;
        }

        dt = MathUtils.Clamp(dt, 0f, 0.1f);
        var flag = false;
        foreach (var particle in Particles)
        {
            if (!particle.IsActive)
            {
                continue;
            }

            particle.Color = particle.BaseColor * MathUtils.Saturate(particle.FadeFactor * particle.TimeToLive);
            particle.TimeToLive -= dt;
            if (particle.TimeToLive <= 0f)
            {
                particle.IsActive = false;
            }
            else
            {
                flag = true;
            }
        }

        if (!flag)
        {
            _isActive = false;
        }

        return false;
    }

    public class Particle : Game.Particle
    {
        public Color BaseColor;

        public float FadeFactor;

        public float TimeToLive;
    }
}
