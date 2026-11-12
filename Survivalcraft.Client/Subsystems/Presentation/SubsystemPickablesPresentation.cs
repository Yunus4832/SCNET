using Engine.Graphics;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Modding.Blocks;

namespace Game.Subsystems;

public sealed class SubsystemPickablesPresentation : Subsystem, IDrawable
{
    private SubsystemPickables _pickables = null!;

    private SubsystemTerrain _subsystemTerrain = null!;

    private SubsystemGameInfo _subsystemGameInfo = null!;

    private SubsystemAudio _subsystemAudio = null!;

    private SubsystemParticles _subsystemParticles = null!;

    private BlockPresentationCatalog _blockPresentations = null!;

    private readonly Dictionary<Pickable, int> _lightSamples = new(ReferenceEqualityComparer.Instance);

    private bool _subscribed;

    private bool _disposed;

    private static readonly int[] _drawOrders = [10];


    private readonly DrawBlockEnvironmentData _drawBlockEnvironmentData = new();


    private PrimitivesRenderer3D PrimitivesRenderer3D => field ??= new();


    private SubsystemSkyPresentation _subsystemSky = null!;


    public int[] DrawOrders => _drawOrders;


    public void Draw(Camera camera, int drawOrder)
    {
        if (_disposed || !WorldRuntimeContext.IsProjectActive(Project)) { return; }

        var totalElapsedGameTime = _subsystemGameInfo.TotalElapsedGameTime;
        _drawBlockEnvironmentData.SubsystemTerrain = _subsystemTerrain;
        var matrix = Matrix.CreateRotationY((float)MathUtils.Remainder(totalElapsedGameTime, 6.2831854820251465));
        var num = MathUtils.Min(_subsystemSky.VisibilityRange, 30f);
        foreach (var pickable in _pickables.Pickables)
        {
            var position = pickable.Position;
            var num2 = Vector3.Dot(camera.ViewDirection, position - camera.ViewPosition);
            if (!(num2 > -0.5f) || !(num2 < num))
            {
                continue;
            }

            var num3 = Terrain.ExtractContents(pickable.Value);
            var num4 = (float)(totalElapsedGameTime - pickable.CreationTime);
            if (!pickable.StuckMatrix.HasValue)
            {
                position.Y += 0.25f * MathUtils.Saturate(3f * num4);
            }

            var x = Terrain.ToCell(position.X);
            var num5 = Terrain.ToCell(position.Y);
            var z = Terrain.ToCell(position.Z);
            var chunkAtCell = _subsystemTerrain.Terrain.GetChunkAtCell(x, z, false);
            if (chunkAtCell is
                {
                    HasContents: true
                } &&
                num5 is >= 0 and < 255 &&
                !IsInsidePredictedOpaqueBlock(
                    _subsystemTerrain.Terrain,
                    new Point3(x, num5, z),
                    _subsystemTerrain.Catalog
                ))
            {
                _drawBlockEnvironmentData.Humidity = _subsystemTerrain.Terrain.GetSeasonalHumidity(x, z);
                _drawBlockEnvironmentData.Temperature = _subsystemTerrain.Terrain.GetSeasonalTemperature(x, z) +
                                                        SubsystemWeather.GetTemperatureAdjustmentAtHeight(num5);
                var f = MathUtils.Max(position.Y - num5 - 0.75f, 0f) / 0.25f;
                var light = (int)MathUtils.Lerp(
                    (LightingManager.SampleEffectiveLight(_subsystemTerrain, x, num5, z) ?? 0),
                    (LightingManager.SampleEffectiveLight(_subsystemTerrain, x, num5 + 1, z) ?? 0),
                    f
                );
                _lightSamples[pickable] = light;
            }

            if (!_lightSamples.TryGetValue(pickable, out var sampledLight))
            {
                continue;
            }

            _drawBlockEnvironmentData.Light = sampledLight;
            _drawBlockEnvironmentData.BillboardDirection = pickable.Position - camera.ViewPosition;
            _drawBlockEnvironmentData.InWorldMatrix.Translation = position;
            if (pickable.StuckMatrix.HasValue)
            {
                var matrix2 = pickable.StuckMatrix.Value;
                _blockPresentations.Get(num3).DrawBlock(
                    PrimitivesRenderer3D,
                    pickable.Value,
                    Color.White,
                    0.3f,
                    ref matrix2,
                    _drawBlockEnvironmentData
                );
            }
            else
            {
                matrix.Translation = position + new Vector3(0f, 0.04f * MathUtils.Sin(3f * num4), 0f);
                _blockPresentations.Get(num3).DrawBlock(
                    PrimitivesRenderer3D,
                    pickable.Value,
                    Color.White,
                    0.3f,
                    ref matrix,
                    _drawBlockEnvironmentData
                );
            }
        }

        PrimitivesRenderer3D.Flush(camera.ViewProjectionMatrix);
    }

    internal static bool IsInsidePredictedOpaqueBlock(Terrain terrain, Point3 cell, BlockContentCatalog catalog)
    {
        var actual = terrain.GetCellValue(cell.X, cell.Y, cell.Z);
        var visual = terrain.GetCellValueForGeometry(cell.X, cell.Y, cell.Z);
        return Terrain.NormalizeCellValue(actual) != Terrain.NormalizeCellValue(visual) &&
               !catalog.Blocks[Terrain.ExtractContents(visual)].Transparent;
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        if (!WorldRuntimeContext.For(Project).HasPresentation)
        {
            throw new InvalidOperationException("Pickable presentation requires a client presentation world.");
        }

        _pickables = Project.FindSubsystem<SubsystemPickables>(true)!;
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true)!;
        _subsystemSky = Project.FindSubsystem<SubsystemSkyPresentation>(true)!;
        _subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true)!;
        _subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true)!;
        _blockPresentations = Project.FindSubsystem<SubsystemBlockPresentation>(true)!.Catalog;
        Subscribe();
    }

    private void Subscribe()
    {
        if (_subscribed) { return; }

        _pickables.PickableRemoved += OnPickableRemoved;
        _pickables.EffectRequested += OnEffectRequested;
        _subscribed = true;
    }

    private void OnPickableRemoved(Pickable pickable) => _lightSamples.Remove(pickable);

    private void OnEffectRequested(PickableEffect effect)
    {
        if (_disposed || !WorldRuntimeContext.IsProjectActive(Project)) { return; }

        switch (effect.Kind)
        {
            case PickableEffectKind.WaterEntry:
                _subsystemParticles.AddParticleSystem(
                    new WaterSplashParticleSystem(_subsystemTerrain, effect.Position, false)
                );
                _subsystemAudio.PlayRandomSound("Audio/Splashes", 1f, effect.Pitch, effect.Position, 6f, true);
                break;
            case PickableEffectKind.MagmaEntry:
                _subsystemParticles.AddParticleSystem(
                    new MagmaSplashParticleSystem(_subsystemTerrain, effect.Position, false)
                );
                _subsystemAudio.PlayRandomSound("Audio/Sizzles", 1f, effect.Pitch, effect.Position, 3f, true);
                break;
            case PickableEffectKind.Burn:
                _subsystemAudio.PlayRandomSound("Audio/Sizzles", 1f, effect.Pitch, effect.Position, 3f, true);
                break;
            case PickableEffectKind.Collected:
                _subsystemAudio.PlaySound("Audio/PickableCollected", 0.7f, effect.Pitch, effect.Position, 2f, false);
                break;
            default:
                throw new InvalidOperationException("Unknown pickable effect.");
        }
    }

    public override void Dispose()
    {
        if (_disposed) { return; }

        _disposed = true;
        if (_subscribed)
        {
            _pickables.PickableRemoved -= OnPickableRemoved;
            _pickables.EffectRequested -= OnEffectRequested;
            _subscribed = false;
        }

        _lightSamples.Clear();
    }
}
