using Engine.Graphics;

namespace Game.ParticleSystems;

public class ParticleSystem<T> : ParticleSystemBase where T : Particle, new()
{
    private TexturedBatch3D? _additiveBatch;

    private TexturedBatch3D? _alphaBlendedBatch;

    private readonly Vector3[] _front = new Vector3[3];

    private readonly Vector3[] _right = new Vector3[3];

    private readonly Vector3[] _up = new Vector3[3];

    protected T[] Particles { get; }

    protected Texture2D Texture
    {
        get => field is not null ? field : throw new InvalidOperationException("Texture is not initialized");
        init
        {
            field = value;
            _additiveBatch = null;
            _alphaBlendedBatch = null;
        }
    } = null!;

    protected int TextureSlotsCount { get; set; }

    protected ParticleSystem(int particlesCount)
    {
        Particles = new T[particlesCount];
        for (var i = 0; i < Particles.Length; i++)
        {
            Particles[i] = new T();
        }
    }

    public override void Draw(Camera camera)
    {
        if (SubsystemParticles is null)
        {
            throw new InvalidOperationException("SubsystemParticles is null");
        }

        if (_additiveBatch == null || _alphaBlendedBatch == null)
        {
            _additiveBatch = SubsystemParticles.PrimitivesRenderer.TexturedBatch(Texture, true, 0,
                DepthStencilState.DepthRead, null, BlendState.Additive, SamplerState.PointClamp);
            _alphaBlendedBatch = SubsystemParticles.PrimitivesRenderer.TexturedBatch(Texture, true, 0,
                DepthStencilState.Default, null, BlendState.AlphaBlend, SamplerState.PointClamp);
        }

        _front[0] = camera.ViewDirection;
        _right[0] = Vector3.Normalize(Vector3.Cross(_front[0], Vector3.UnitY));
        _up[0] = Vector3.Normalize(Vector3.Cross(_right[0], _front[0]));
        _front[1] = camera.ViewDirection;
        _right[1] = Vector3.Normalize(Vector3.Cross(_front[1], Vector3.UnitY));
        _up[1] = Vector3.UnitY;
        _front[2] = Vector3.UnitY;
        _right[2] = Vector3.UnitX;
        _up[2] = Vector3.UnitZ;
        var s = 1f / TextureSlotsCount;
        foreach (var particle in Particles)
        {
            if (!particle.IsActive)
            {
                continue;
            }

            var position = particle.Position;
            var size = particle.Size;
            var rotation = particle.Rotation;
            var textureSlot = particle.TextureSlot;
            Vector3 p;
            Vector3 p2;
            Vector3 p3;
            Vector3 p4;
            if (particle.BillboardingMode == ParticleBillboardingMode.None)
            {
                p = position + (-particle.Right - particle.Up);
                p2 = position + (particle.Right - particle.Up);
                p3 = position + (particle.Right + particle.Up);
                p4 = position + (-particle.Right + particle.Up);
            }
            else if (particle.BillboardingMode == ParticleBillboardingMode.Horizontal && rotation != 0f)
            {
                var vector = new Vector3(MathUtils.Cos(rotation), 0f, MathUtils.Sin(rotation));
                var vector2 = new Vector3(vector.Z, 0f, 0f - vector.X);
                vector2 *= size.Y;
                vector *= size.X;
                p = position + (-vector2 - vector);
                p2 = position + (vector2 - vector);
                p3 = position + (vector2 + vector);
                p4 = position + (-vector2 + vector);
            }
            else if (rotation != 0f)
            {
                var v = _front[(uint)particle.BillboardingMode];
                var v2 = v.X * v.X > v.Z * v.Z
                    ? new Vector3(0f, MathUtils.Cos(rotation), MathUtils.Sin(rotation))
                    : new Vector3(MathUtils.Sin(rotation), MathUtils.Cos(rotation), 0f);
                var vector3 = Vector3.Normalize(Vector3.Cross(v, v2));
                v2 = Vector3.Normalize(Vector3.Cross(v, vector3));
                vector3 *= size.Y;
                v2 *= size.X;
                p = position + (-vector3 - v2);
                p2 = position + (vector3 - v2);
                p3 = position + (vector3 + v2);
                p4 = position + (-vector3 + v2);
            }
            else
            {
                var vector4 = _right[(uint)particle.BillboardingMode];
                var vector5 = _up[(uint)particle.BillboardingMode];
                var vector6 = vector4 * size.X;
                var vector7 = vector5 * size.Y;
                p = position + (-vector6 - vector7);
                p2 = position + (vector6 - vector7);
                p3 = position + (vector6 + vector7);
                p4 = position + (-vector6 + vector7);
            }

            var obj = particle.UseAdditiveBlending ? _additiveBatch : _alphaBlendedBatch;
            var v3 = new Vector2(textureSlot % TextureSlotsCount, textureSlot / TextureSlotsCount);
            var num = 0f;
            var num2 = 1f;
            var num3 = 1f;
            var num4 = 0f;
            if (particle.FlipX)
            {
                num = 1f - num;
                num2 = 1f - num2;
            }

            if (particle.FlipY)
            {
                num3 = 1f - num3;
                num4 = 1f - num4;
            }

            var texCoord = (v3 + new Vector2(num, num3)) * s;
            var texCoord2 = (v3 + new Vector2(num2, num3)) * s;
            var texCoord3 = (v3 + new Vector2(num2, num4)) * s;
            var texCoord4 = (v3 + new Vector2(num, num4)) * s;
            obj.QueueQuad(p, p2, p3, p4, texCoord, texCoord2, texCoord3, texCoord4, particle.Color);
        }
    }

    public override bool Simulate(float dt)
    {
        return false;
    }
}
