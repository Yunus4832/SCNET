using Engine.Graphics;

using Game.Terrains.Lighting;

namespace Game.ParticleSystems;

public class ParticleSystem<T> : ParticleSystemBase where T : Particle, new()
{
    private TexturedBatch3D? _additiveBatch;

    private TexturedBatch3D? _alphaBlendedBatch;

    private readonly Vector3[] _front = new Vector3[3];

    private readonly Vector3[] _right = new Vector3[3];

    private readonly Vector3[] _up = new Vector3[3];

    private SubsystemTerrain? _lightTerrain;

    private Vector3 _lightOrigin;

    private TerrainLightChannels _spatialLight;

    protected void SetLightOrigin(SubsystemTerrain terrain, Vector3 position)
    {
        _lightTerrain = terrain;
        _lightOrigin = position;
    }

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
            _additiveBatch = SubsystemParticles.PrimitivesRenderer.TexturedBatch(
                Texture,
                true,
                0,
                DepthStencilState.DepthRead,
                null,
                BlendState.PremultipliedAdditive,
                SamplerState.PointClamp
            );
            _alphaBlendedBatch = SubsystemParticles.PrimitivesRenderer.TexturedBatch(
                Texture,
                true,
                0,
                DepthStencilState.Default,
                null,
                BlendState.AlphaBlend,
                SamplerState.PointClamp
            );
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
        var light = 1f;
        if (_lightTerrain != null)
        {
            _spatialLight = LightingManager.SampleNeighborLight(_lightTerrain, _lightOrigin) ?? _spatialLight;
            light = LightingManager.GetBrightness(
                LightingManager.GetEffectiveLight(_spatialLight, LightingManager.CaptureSkyLightValue(_lightTerrain))
            );
        }

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
                var perpendicularExtent = new Vector3(vector.Z, 0f, 0f - vector.X);
                perpendicularExtent *= size.Y;
                vector *= size.X;
                p = position + (-perpendicularExtent - vector);
                p2 = position + (perpendicularExtent - vector);
                p3 = position + (perpendicularExtent + vector);
                p4 = position + (-perpendicularExtent + vector);
            }
            else if (rotation != 0f)
            {
                var v = _front[(uint)particle.BillboardingMode];
                var v2 = v.X * v.X > v.Z * v.Z
                    ? new Vector3(0f, MathUtils.Cos(rotation), MathUtils.Sin(rotation))
                    : new Vector3(MathUtils.Sin(rotation), MathUtils.Cos(rotation), 0f);
                var rotatedUp = Vector3.Normalize(Vector3.Cross(v, v2));
                v2 = Vector3.Normalize(Vector3.Cross(v, rotatedUp));
                rotatedUp *= size.Y;
                v2 *= size.X;
                p = position + (-rotatedUp - v2);
                p2 = position + (rotatedUp - v2);
                p3 = position + (rotatedUp + v2);
                p4 = position + (-rotatedUp + v2);
            }
            else
            {
                var billboardRight = _right[(uint)particle.BillboardingMode];
                var billboardUp = _up[(uint)particle.BillboardingMode];
                var horizontalExtent = billboardRight * size.X;
                var verticalExtent = billboardUp * size.Y;
                p = position + (-horizontalExtent - verticalExtent);
                p2 = position + (horizontalExtent - verticalExtent);
                p3 = position + (horizontalExtent + verticalExtent);
                p4 = position + (-horizontalExtent + verticalExtent);
            }

            var obj = particle.UseAdditiveBlending ? _additiveBatch : _alphaBlendedBatch;
            var v3 = new Vector2(textureSlot % TextureSlotsCount, textureSlot / TextureSlotsCount);
            var leftU = 0f;
            var rightU = 1f;
            var bottomV = 1f;
            var topV = 0f;
            if (particle.FlipX)
            {
                leftU = 1f - leftU;
                rightU = 1f - rightU;
            }

            if (particle.FlipY)
            {
                bottomV = 1f - bottomV;
                topV = 1f - topV;
            }

            var texCoord = (v3 + new Vector2(leftU, bottomV)) * s;
            var texCoord2 = (v3 + new Vector2(rightU, bottomV)) * s;
            var texCoord3 = (v3 + new Vector2(rightU, topV)) * s;
            var texCoord4 = (v3 + new Vector2(leftU, topV)) * s;
            obj.QueueQuad(
                p,
                p2,
                p3,
                p4,
                texCoord,
                texCoord2,
                texCoord3,
                texCoord4,
                Color.MultiplyColorOnly(particle.Color, light)
            );
        }
    }

    public override bool Simulate(float dt)
    {
        return false;
    }
}
