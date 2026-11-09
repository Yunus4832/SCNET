using Engine.Graphics;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Modding.Blocks;

namespace Game.Subsystems;

public sealed class SubsystemProjectilesPresentation : Subsystem, IDrawable, IUpdateable
{
    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Presentation;

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    private SubsystemProjectiles _projectiles = null!;

    private SubsystemTerrain _subsystemTerrain = null!;

    private SubsystemGameInfo _subsystemGameInfo = null!;

    private SubsystemAudio _subsystemAudio = null!;

    private SubsystemParticles _subsystemParticles = null!;

    private SubsystemSoundMaterials _subsystemSoundMaterials = null!;

    private BlockPresentationCatalog _blockPresentations = null!;

    private readonly Dictionary<Projectile, int> _lightSamples = new(ReferenceEqualityComparer.Instance);

    private readonly Dictionary<Projectile, TrailState> _trails = new(ReferenceEqualityComparer.Instance);

    private bool _subscribed;

    private bool _disposed;

    private sealed record TrailState(ProjectileTrailDescription Description, ITrailParticleSystem Particles);

    private static readonly int[] _drawOrders = [10];


    private readonly DrawBlockEnvironmentData _drawBlockEnvironmentData = new();


    private PrimitivesRenderer3D PrimitivesRenderer3D => field ??= new();


    private SubsystemSkyPresentation _subsystemSky = null!;


    public int[] DrawOrders => _drawOrders;


    public void Draw(Camera camera, int drawOrder)
    {
        if (_disposed || !WorldRuntimeContext.IsProjectActive(Project)) { return; }

        _drawBlockEnvironmentData.SubsystemTerrain = _subsystemTerrain;
        _drawBlockEnvironmentData.InWorldMatrix = Matrix.Identity;
        var num = MathUtils.Sqr(_subsystemSky.VisibilityRange);
        foreach (var projectile in _projectiles.Projectiles)
        {
            var position = projectile.Position;
            if (projectile.NoChunk ||
                !(Vector3.DistanceSquared(camera.ViewPosition, position) < num) ||
                !camera.ViewFrustum.Intersection(position))
            {
                continue;
            }

            var x = Terrain.ToCell(position.X);
            var num2 = Terrain.ToCell(position.Y);
            var z = Terrain.ToCell(position.Z);
            var num3 = Terrain.ExtractContents(projectile.Value);
            var block = WorldContentBinding.For(Project).Catalog.Blocks[num3];
            var chunkAtCell = _subsystemTerrain.Terrain.GetChunkAtCell(x, z, false);
            if (chunkAtCell is { HasCurrentLight: true } && num2 is >= 0 and < 255)
            {
                _drawBlockEnvironmentData.Humidity = _subsystemTerrain.Terrain.GetSeasonalHumidity(x, z);
                _drawBlockEnvironmentData.Temperature = _subsystemTerrain.Terrain.GetSeasonalTemperature(x, z) +
                                                        SubsystemWeather.GetTemperatureAdjustmentAtHeight(num2);
                _lightSamples[projectile] = _subsystemTerrain.Terrain.GetCellLightFast(x, num2, z);
            }

            _drawBlockEnvironmentData.Light = _lightSamples.GetValueOrDefault(projectile);
            _drawBlockEnvironmentData.BillboardDirection =
                block.AlignToVelocity ? null : new Vector3?(camera.ViewDirection);
            _drawBlockEnvironmentData.InWorldMatrix.Translation = position;
            Matrix matrix;
            if (block.AlignToVelocity)
            {
                matrix = ProjectileMotionMath.CreateVelocityAlignment(position, projectile.Velocity);
            }
            else if (projectile.Rotation != Vector3.Zero)
            {
                matrix = Matrix.CreateFromAxisAngle(
                    Vector3.Normalize(projectile.Rotation),
                    projectile.Rotation.Length()
                );
                matrix.Translation = projectile.Position;
            }
            else
            {
                matrix = Matrix.CreateTranslation(projectile.Position);
            }

            _blockPresentations.Get(num3).DrawBlock(
                PrimitivesRenderer3D,
                projectile.Value,
                Color.White,
                0.3f,
                ref matrix,
                _drawBlockEnvironmentData
            );
        }

        PrimitivesRenderer3D.Flush(camera.ViewProjectionMatrix);
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        if (!WorldRuntimeContext.For(Project).HasPresentation)
        {
            throw new InvalidOperationException("Projectile presentation requires a client presentation world.");
        }

        _projectiles = Project.FindSubsystem<SubsystemProjectiles>(true)!;
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true)!;
        _subsystemSky = Project.FindSubsystem<SubsystemSkyPresentation>(true)!;
        _subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true)!;
        _subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true)!;
        _subsystemSoundMaterials = Project.FindSubsystem<SubsystemSoundMaterials>(true)!;
        _blockPresentations = Project.FindSubsystem<SubsystemBlockPresentation>(true)!.Catalog;
        Subscribe();
        foreach (var projectile in _projectiles.Projectiles) { EnsureTrail(projectile); }
    }

    private void Subscribe()
    {
        if (_subscribed) { return; }

        _projectiles.ProjectileAdded += OnProjectileAdded;
        _projectiles.ProjectileRemoved += OnProjectileRemoved;
        _projectiles.TrailChanged += OnTrailChanged;
        _projectiles.EffectRequested += OnEffectRequested;
        _subscribed = true;
    }

    private void OnProjectileAdded(Projectile projectile) => EnsureTrail(projectile);

    private void OnTrailChanged(Projectile projectile)
    {
        if (!_projectiles.Projectiles.Contains(projectile)) { return; }

        RemoveVisualTrail(projectile);
        EnsureTrail(projectile);
    }

    private void OnProjectileRemoved(Projectile projectile)
    {
        RemoveVisualTrail(projectile);
        _lightSamples.Remove(projectile);
    }

    private void EnsureTrail(Projectile projectile)
    {
        if (projectile.Trail is not { } description || _trails.ContainsKey(projectile)) { return; }

        var remaining = description.EndTime - _subsystemGameInfo.TotalElapsedGameTime;
        if (remaining <= 0) { return; }

        ITrailParticleSystem particles = description.Kind switch
        {
            ProjectileTrailKind.Smoke => new SmokeTrailParticleSystem(
                description.ParticleCount,
                description.Size,
                (float)remaining,
                description.Color
            ),
            ProjectileTrailKind.Fireworks => new FireworksTrailParticleSystem(),
            _ => throw new InvalidOperationException("Unknown projectile trail description.")
        };
        _trails.Add(projectile, new TrailState(description, particles));
    }

    public void Update(float dt)
    {
        if (_disposed || !WorldRuntimeContext.IsProjectActive(Project)) { return; }

        foreach (var projectile in _projectiles.Projectiles)
        {
            EnsureTrail(projectile);
            if (!_trails.TryGetValue(projectile, out var state)) { continue; }

            var expired = state.Description.EndTime <= _subsystemGameInfo.TotalElapsedGameTime;
            state.Particles.IsStopped = projectile.NoChunk || projectile.IsInWater || projectile.ToRemove || expired;
            if (!expired &&
                !projectile.NoChunk &&
                !_subsystemParticles.ContainsParticleSystem((ParticleSystemBase)state.Particles))
            {
                _subsystemParticles.AddParticleSystem((ParticleSystemBase)state.Particles);
            }

            var rotation = projectile.Rotation.LengthSquared() > 0f
                ? Matrix.CreateFromAxisAngle(Vector3.Normalize(projectile.Rotation), projectile.Rotation.Length())
                : Matrix.Identity;
            state.Particles.Position =
                projectile.Position + Vector3.TransformNormal(state.Description.Offset, rotation);
        }
    }

    private void RemoveVisualTrail(Projectile projectile)
    {
        if (!_trails.Remove(projectile, out var state)) { return; }

        state.Particles.IsStopped = true;
        if (_subsystemParticles.ContainsParticleSystem((ParticleSystemBase)state.Particles))
        {
            _subsystemParticles.RemoveParticleSystem((ParticleSystemBase)state.Particles);
        }
    }

    private void OnEffectRequested(ProjectileEffect effect)
    {
        if (_disposed || !WorldRuntimeContext.IsProjectActive(Project)) { return; }

        switch (effect.Kind)
        {
            case ProjectilePackage.EffectType.Impact:
                _subsystemSoundMaterials.PlayImpactSound(effect.Value, effect.Position, 1f);
                break;
            case ProjectilePackage.EffectType.Debris:
                if (_blockPresentations.Get(Terrain.ExtractContents(effect.Value))
                        .CreateDebrisParticleSystem(_subsystemTerrain, effect.Position, effect.Value, 1f) is { } debris)
                {
                    _subsystemParticles.AddParticleSystem(debris);
                }

                break;
            case ProjectilePackage.EffectType.WaterSplash:
                _subsystemParticles.AddParticleSystem(
                    new WaterSplashParticleSystem(_subsystemTerrain, effect.Position, false)
                );
                _subsystemAudio.PlayRandomSound("Audio/Splashes", 1f, effect.Pitch, effect.Position, 6f, true);
                break;
            case ProjectilePackage.EffectType.MagmaSplash:
                _subsystemParticles.AddParticleSystem(
                    new MagmaSplashParticleSystem(_subsystemTerrain, effect.Position, false)
                );
                _subsystemAudio.PlayRandomSound("Audio/Sizzles", 1f, effect.Pitch, effect.Position, 3f, true);
                break;
            case ProjectilePackage.EffectType.FireworksWhoosh:
                _subsystemAudio.PlayRandomSound("Audio/FireworksWhoosh", 1f, effect.Pitch, effect.Position, 8f, true);
                break;
            case ProjectilePackage.EffectType.FireworksExplosion:
            {
                var data = effect.Value;
                var flickering = FireworksBlock.GetFlickering(data) ? 0.66f : 0f;
                var particleSize = FireworksBlock.GetAltitude(data) > 0 ? 1.1f : 1f;
                var color = FireworksBlock.FireworksColors[FireworksBlock.GetColor(data)];
                _subsystemParticles.AddParticleSystem(
                    new FireworksParticleSystem(
                        effect.Position,
                        color,
                        FireworksBlock.GetShape(data),
                        flickering,
                        particleSize
                    )
                );
                _subsystemAudio.PlayRandomSound("Audio/FireworksPop", 1f, effect.Pitch, effect.Position, 80f, true);
                break;
            }
            case ProjectilePackage.EffectType.Sizzle:
                _subsystemAudio.PlayRandomSound("Audio/Sizzles", 1f, effect.Pitch, effect.Position, 3f, true);
                break;
            default:
                throw new InvalidOperationException("Unknown projectile effect.");
        }
    }

    public override void Dispose()
    {
        if (_disposed) { return; }

        _disposed = true;
        if (_subscribed)
        {
            _projectiles.ProjectileAdded -= OnProjectileAdded;
            _projectiles.ProjectileRemoved -= OnProjectileRemoved;
            _projectiles.TrailChanged -= OnTrailChanged;
            _projectiles.EffectRequested -= OnEffectRequested;
            _subscribed = false;
        }

        foreach (var projectile in _trails.Keys.ToArray()) { RemoveVisualTrail(projectile); }

        _lightSamples.Clear();
    }
}
