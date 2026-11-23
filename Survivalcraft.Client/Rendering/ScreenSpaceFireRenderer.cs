using Engine.Graphics;

namespace Game.Rendering;

public class ScreenSpaceFireRenderer
{
    private readonly List<Particle> _particles = [];

    private readonly Random _random = new();

    private readonly Texture2D _texture;

    private float _toGenerate;

    public ScreenSpaceFireRenderer(int particlesCount)
    {
        _texture = AssetManager.Get<Texture2D>("Textures/FireParticle");
        for (var i = 0; i < particlesCount; i++)
        {
            _particles.Add(new Particle());
        }
    }

    public float ParticlesPerSecond { get; set; }

    public float ParticleSpeed { get; set; }

    public float MinTimeToLive { get; set; }

    public float MaxTimeToLive { get; set; }

    public float ParticleSize { get; set; }

    public float ParticleAnimationPeriod { get; set; }

    public float ParticleAnimationOffset { get; set; }

    public Vector2 Origin { get; set; }

    public float Width { get; set; }

    public float CutoffPosition { get; set; }

    public void Update(float dt)
    {
        _toGenerate += ParticlesPerSecond * dt;
        foreach (var particle in _particles)
        {
            if (particle.Active)
            {
                particle.Position.Y += particle.Speed * dt;
                particle.AnimationTime += dt;
                particle.TimeToLive -= dt;
                particle.TextureSlot = (int)MathUtils.Max(9f * particle.AnimationTime / ParticleAnimationPeriod, 0f);
                if (particle.TimeToLive <= 0f || particle.TextureSlot > 8 || particle.Position.Y < CutoffPosition)
                {
                    particle.Active = false;
                }
            }
            else if (_toGenerate >= 1f)
            {
                particle.Active = true;
                particle.Position = new Vector2(_random.Float(Origin.X, Origin.X + Width), Origin.Y);
                particle.Size = new Vector2(ParticleSize);
                particle.Speed = (0f - _random.Float(0.75f, 1.25f)) * ParticleSpeed;
                particle.AnimationTime = _random.Float(0f, ParticleAnimationOffset);
                particle.TimeToLive = MathUtils.Lerp(MinTimeToLive, MaxTimeToLive, _random.Float(0f, 1f));
                particle.FlipX = _random.Int(0, 1) == 0;
                particle.FlipY = _random.Int(0, 1) == 0;
                _toGenerate -= 1f;
            }
        }

        _toGenerate = MathUtils.Remainder(_toGenerate, 1f);
    }

    public void Draw(PrimitivesRenderer2D primitivesRenderer, float depth, Matrix matrix, Color color)
    {
        var texturedBatch2D = primitivesRenderer.TexturedBatch(
            _texture,
            false,
            0,
            DepthStencilState.None,
            null,
            null,
            SamplerState.PointClamp
        );
        var count = texturedBatch2D.TriangleVertices.Count;
        foreach (var particle in _particles)
        {
            if (particle.Active)
            {
                DrawParticle(texturedBatch2D, particle, depth, color);
            }
        }

        texturedBatch2D.TransformTriangles(matrix, count);
    }

    private void DrawParticle(TexturedBatch2D batch, Particle particle, float depth, Color color)
    {
        var corner = particle.Position - particle.Size / 2f;
        var corner2 = particle.Position + particle.Size / 2f;
        var textureSlot = particle.TextureSlot;
        var v = new Vector2(textureSlot % 3, textureSlot / 3);
        var leftU = 0f;
        var rightU = 1f;
        var topV = 0f;
        var bottomV = 1f;
        if (particle.FlipX)
        {
            leftU = 1f - leftU;
            rightU = 1f - rightU;
        }

        if (particle.FlipY)
        {
            topV = 1f - topV;
            bottomV = 1f - bottomV;
        }

        var texCoord = (v + new Vector2(leftU, topV)) * 0.333333343f;
        var texCoord2 = (v + new Vector2(rightU, bottomV)) * 0.333333343f;
        batch.QueueQuad(corner, corner2, depth, texCoord, texCoord2, color);
    }

    private class Particle
    {
        public bool Active;

        public float AnimationTime;

        public bool FlipX;

        public bool FlipY;

        public Vector2 Position;

        public Vector2 Size;

        public float Speed;

        public int TextureSlot;

        public float TimeToLive;
    }
}
