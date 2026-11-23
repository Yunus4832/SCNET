using Engine.Graphics;

namespace Game.ParticleSystems;

public class HitValueParticleSystem : ParticleSystem<HitValueParticleSystem.Particle>
{
    private FontBatch3D? _batch;

    public HitValueParticleSystem(
        Vector3 position,
        Vector3 velocity,
        Color color,
        string text
    ) : base(1)
    {
        var random = new Random();
        var obj = Particles[0];
        obj.IsActive = true;
        obj.Position = position;
        obj.TimeToLive = 0.9f;
        obj.Velocity = velocity + random.Vector3(0.75f) * new Vector3(1f, 0f, 1f) + 0.5f * Vector3.UnitY;
        obj.BaseColor = color;
        obj.Text = text;
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
                particle.Velocity += new Vector3(0f, 0.5f, 0f) * dt;
                particle.Velocity *= velocityDamping;
                particle.Position += particle.Velocity * dt;
                particle.Color = particle.BaseColor * MathUtils.Saturate(2f * particle.TimeToLive);
            }
            else
            {
                particle.IsActive = false;
            }
        }

        return !hasActiveParticles;
    }

    public override void Draw(Camera camera)
    {
        if (SubsystemParticles is null)
        {
            throw new InvalidOperationException("SubsystemParticles is null");
        }

        _batch ??= SubsystemParticles.PrimitivesRenderer.FontBatch(
            AssetManager.Get<BitmapFont>("Fonts/Pericles"),
            0,
            DepthStencilState.None
        );
        var viewDirection = camera.ViewDirection;
        var vector = Vector3.Normalize(Vector3.Cross(viewDirection, Vector3.UnitY));
        var v = -Vector3.Normalize(Vector3.Cross(vector, viewDirection));
        foreach (var particle in Particles)
        {
            if (!particle.IsActive)
            {
                continue;
            }

            var viewDistance = Vector3.Distance(camera.ViewPosition, particle.Position);
            var nearFade = MathUtils.Saturate(3f * (viewDistance - 0.2f));
            var farFade = MathUtils.Saturate(0.2f * (20f - viewDistance));
            var distanceFade = nearFade * farFade;
            if (!(distanceFade > 0f))
            {
                continue;
            }

            var s = 0.006f * MathUtils.Sqrt(viewDistance);
            var color = particle.Color * distanceFade;
            _batch.QueueText(
                particle.Text,
                particle.Position,
                vector * s,
                v * s,
                color,
                TextAnchor.HorizontalCenter | TextAnchor.VerticalCenter,
                Vector2.Zero
            );
        }
    }

    public class Particle : Game.ParticleSystems.Particle
    {
        public Color BaseColor;

        public string Text = string.Empty;

        public float TimeToLive;

        public Vector3 Velocity;
    }
}
