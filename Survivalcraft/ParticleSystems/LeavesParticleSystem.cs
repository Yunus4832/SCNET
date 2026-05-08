using Engine.Graphics;

namespace Game.ParticleSystems;

public class LeavesParticleSystem : ParticleSystem<LeavesParticleSystem.Particle>
{
    private readonly bool _fadeIn;

    private readonly Point3 _point;

    private readonly Random _random = new();

    private readonly SubsystemTerrain _subsystemTerrain;

    private bool _createFallenLeaves;

    public LeavesParticleSystem(
        SubsystemTerrain subsystemTerrain,
        Point3 point,
        int leavesCount,
        bool fadeIn,
        bool createFallenLeaves,
        int value
    ) : base(leavesCount)
    {
        _subsystemTerrain = subsystemTerrain;
        _point = point;
        _fadeIn = fadeIn;
        _createFallenLeaves = createFallenLeaves;
        Texture = ContentManager.Get<Texture2D>("Textures/LeafParticle");
        TextureSlotsCount = 1;
        var color = BlocksManager.Blocks[Terrain.ExtractContents(value)] is LeavesBlock leavesBlock
            ? leavesBlock.GetLeavesBlockColor(value, _subsystemTerrain.Terrain, point.X, point.Y, point.Z)
            : Color.Transparent;
        foreach (var particle in Particles)
        {
            var f = _random.Float();
            var color2 = Color.Lerp(new Color(180, 120, 120), new Color(200, 255, 255), _random.Float(0f, 1f));
            particle.IsActive = true;
            particle.EndTime = 12f;
            particle.Position = new Vector3(point) + new Vector3(0.5f) + 0.45f * new Vector3(_random.Float(-1f, 1f),
                MathUtils.Lerp(1f, -1f, f), _random.Float(-1f, 1f));
            particle.Light = 7;
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
        var flag = false;
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
                var vector2 = Vector2.Rotate(Vector2.UnitY, particle.Angle);
                particle.Right = particle.Size.X * new Vector3(v.X * vector2.X, v.Y, v.X * vector2.Y);
                particle.Up = particle.Size.Y * new Vector3(vector.X * vector2.X, vector.Y, vector.X * vector2.Y);
                var num = MathUtils.Saturate(4f * particle.Time);
                particle.Position += 0.8f * particle.Speed * num * dt * new Vector3(0f, -1f, 0f);
                particle.Position += 0.4f * particle.Speed * num * dt * MathUtils.Cos(particle.Phase) *
                                     new Vector3(vector2.X, 0f, vector2.Y);
                particle.Position += 0.3f * particle.Speed * num * dt * MathUtils.Cos(2f * particle.Phase) *
                                     new Vector3(0f, -1f, 0f);
            }

            var num2 = Terrain.ToCell(particle.Position.X);
            var num3 = Terrain.ToCell(particle.Position.Y);
            var num4 = Terrain.ToCell(particle.Position.Z);
            var chunkAtCell = terrain.GetChunkAtCell(num2, num4, false);
            if (chunkAtCell is { State: >= TerrainChunkState.InvalidVertices1 })
            {
                particle.Light = terrain.GetCellLight(num2, num3, num4);
            }

            particle.Color = Color.MultiplyColorOnlyNotSaturated(particle.BaseColor,
                LightingManager.LightIntensityByLightValue[particle.Light]);
            var num5 = MathUtils.Saturate(0.5f * (particle.EndTime - particle.Time));
            if (_fadeIn)
            {
                num5 *= MathUtils.Saturate(1f * particle.Time);
            }

            particle.Color *= num5;
            if (particle.BillboardingMode == ParticleBillboardingMode.None)
            {
                var cellValue = terrain.GetCellValue(num2, num3, num4);
                var num6 = Terrain.ExtractContents(cellValue);
                var block = BlocksManager.Blocks[num6];
                if (block is WaterBlock)
                {
                    particle.EndTime = particle.Time;
                }
                else if (block.Collidable && !(block is LeavesBlock))
                {
                    const float num7 = 0.5f;
                    var ray = new Ray3(particle.Position - new Vector3(num2, num3, num4) + new Vector3(0f, num7, 0f),
                        -Vector3.UnitY);
                    var num8 = block.Raycast(ray, _subsystemTerrain, cellValue, false, out _, out _);
                    if (num8 is < num7 - 0f)
                    {
                        particle.BillboardingMode = ParticleBillboardingMode.Horizontal;
                        particle.Position = ray.Sample(num8.Value) + new Vector3(num2, num3 + 0.03f, num4);
                        particle.EndTime = particle.Time + 2f;
                        if (_createFallenLeaves)
                        {
                            _createFallenLeaves = false;
                            _subsystemTerrain.Project.FindSubsystem<SubsystemDeciduousLeavesBlockBehavior>(true)!
                                .CreateFallenLeaves(_point, true);
                        }
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
                flag = true;
            }
        }

        return !flag;
    }

    public class Particle : Game.Particle
    {
        public float Angle;

        public float AngleSpeed;

        public Color BaseColor;

        public float EndTime;

        public int Light;

        public float Phase;

        public float PhaseSpeed;

        public float Speed;

        public float Time;
    }
}
