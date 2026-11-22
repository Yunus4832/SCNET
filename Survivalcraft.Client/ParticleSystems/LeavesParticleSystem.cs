using Engine.Graphics;

using Game.Terrains.Lighting;

namespace Game.ParticleSystems;

public class LeavesParticleSystem : ParticleSystem<LeavesParticleSystem.Particle>
{
    private readonly bool _fadeIn;


    private readonly Random _random = new();

    private readonly SubsystemTerrain _subsystemTerrain;


    public LeavesParticleSystem(
        SubsystemTerrain subsystemTerrain,
        Point3 point,
        int leavesCount,
        bool fadeIn,
        int value
    ) : base(leavesCount)
    {
        _subsystemTerrain = subsystemTerrain;
        _fadeIn = fadeIn;
        Texture = ContentManager.Get<Texture2D>("Textures/LeafParticle");
        TextureSlotsCount = 1;
        var color =
            _subsystemTerrain.Project.FindSubsystem<SubsystemBlockPresentation>(true)!.Catalog
                .Get(Terrain.ExtractContents(value)) is Blocks.Presentation.LeavesBlockPresentation leavesBlock
                ? leavesBlock.GetLeavesBlockColor(value, _subsystemTerrain.Terrain, point.X, point.Y, point.Z)
                : Color.Transparent;
        foreach (var particle in Particles)
        {
            var f = _random.Float();
            var color2 = Color.Lerp(new Color(180, 120, 120), new Color(200, 255, 255), _random.Float(0f, 1f));
            particle.IsActive = true;
            particle.EndTime = 12f;
            particle.Position = new Vector3(point) +
                                new Vector3(0.5f) +
                                0.45f *
                                new Vector3(
                                    _random.Float(-1f, 1f),
                                    MathUtils.Lerp(1f, -1f, f),
                                    _random.Float(-1f, 1f)
                                );
            particle.Light = new TerrainLightChannels(0, 7);
            particle.BaseColor = color * color2;
            particle.Color = Color.Transparent;
            particle.BillboardingMode = ParticleBillboardingMode.None;
            particle.Size = new Vector2(0.18f) * _random.Float(0.75f, 1f);
            particle.Speed = MathUtils.Lerp(1.5f, 3.5f, f);
            particle.Phase = _random.Float(0f, (float)Math.PI * 2f);
            particle.PhaseSpeed = 2f * particle.Speed * _random.Float(0.75f, 1.25f);
            particle.Angle = _random.Float(0f, (float)Math.PI * 2f);
            particle.AngleSpeed = _random.Sign() * _random.Float(1f, 3f);
            particle.FlipX = _random.Bool();
            particle.FlipY = _random.Bool();
        }
    }

    public override bool Simulate(float dt)
    {
        var terrain = _subsystemTerrain.Terrain;
        var daylight = LightingManager.CaptureSkyLightValue(_subsystemTerrain);
        var hasActiveParticles = false;
        foreach (var particle in Particles)
        {
            if (!particle.IsActive)
            {
                continue;
            }

            if (particle.BillboardingMode == ParticleBillboardingMode.None)
            {
                particle.Phase += particle.PhaseSpeed * dt;
                particle.Rotation = -0.5f * MathUtils.Sin(particle.Phase) + (float)Math.PI / 2f;
                particle.Angle += particle.AngleSpeed * dt;
                var v = Vector2.Rotate(Vector2.UnitX, particle.Rotation);
                var vector = Vector2.Perpendicular(v);
                var flutterDirection = Vector2.Rotate(Vector2.UnitY, particle.Angle);
                particle.Right = particle.Size.X * new Vector3(v.X * flutterDirection.X, v.Y, v.X * flutterDirection.Y);
                particle.Up = particle.Size.Y *
                              new Vector3(vector.X * flutterDirection.X, vector.Y, vector.X * flutterDirection.Y);
                var motionRamp = MathUtils.Saturate(4f * particle.Time);
                particle.Position += 0.8f * particle.Speed * motionRamp * dt * new Vector3(0f, -1f, 0f);
                particle.Position += 0.4f *
                                     particle.Speed *
                                     motionRamp *
                                     dt *
                                     MathUtils.Cos(particle.Phase) *
                                     new Vector3(flutterDirection.X, 0f, flutterDirection.Y);
                particle.Position += 0.3f *
                                     particle.Speed *
                                     motionRamp *
                                     dt *
                                     MathUtils.Cos(2f * particle.Phase) *
                                     new Vector3(0f, -1f, 0f);
            }

            var cellX = Terrain.ToCell(particle.Position.X);
            var cellY = Terrain.ToCell(particle.Position.Y);
            var cellZ = Terrain.ToCell(particle.Position.Z);
            particle.Light = LightingManager.SampleSpatialLight(_subsystemTerrain, cellX, cellY, cellZ) ??
                             particle.Light;

            particle.Color = Color.MultiplyColorOnlyNotSaturated(
                particle.BaseColor,
                LightingManager.GetBrightness(LightingManager.GetEffectiveLight(particle.Light, daylight))
            );
            var fadeFactor = MathUtils.Saturate(0.5f * (particle.EndTime - particle.Time));
            if (_fadeIn)
            {
                fadeFactor *= MathUtils.Saturate(1f * particle.Time);
            }

            particle.Color *= fadeFactor;
            if (particle.BillboardingMode == ParticleBillboardingMode.None)
            {
                var cellValue = terrain.GetCellValue(cellX, cellY, cellZ);
                var contents = Terrain.ExtractContents(cellValue);
                var block = _subsystemTerrain.Catalog.Blocks[contents];
                if (block is WaterBlock)
                {
                    particle.EndTime = particle.Time;
                }
                else if (block.Collidable && !(block is LeavesBlock))
                {
                    const float rayStartOffset = 0.5f;
                    var ray = new Ray3(
                        particle.Position - new Vector3(cellX, cellY, cellZ) + new Vector3(0f, rayStartOffset, 0f),
                        -Vector3.UnitY
                    );
                    var hitDistance = block.Raycast(ray, _subsystemTerrain, cellValue, false, out _, out _);
                    if (hitDistance is < rayStartOffset - 0f)
                    {
                        particle.BillboardingMode = ParticleBillboardingMode.Horizontal;
                        particle.Position = ray.Sample(hitDistance.Value) + new Vector3(cellX, cellY + 0.03f, cellZ);
                        particle.EndTime = particle.Time + 2f;
                    }
                }
            }

            particle.Time += dt;
            if (particle.Time >= particle.EndTime)
            {
                particle.IsActive = false;
            }
            else
            {
                hasActiveParticles = true;
            }
        }

        return !hasActiveParticles;
    }

    public class Particle : Game.ParticleSystems.Particle
    {
        public float Angle;

        public float AngleSpeed;

        public Color BaseColor;

        public float EndTime;

        public TerrainLightChannels Light;

        public float Phase;

        public float PhaseSpeed;

        public float Speed;

        public float Time;
    }
}
