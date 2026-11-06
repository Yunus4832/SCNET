using Engine.Audio;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems;

public sealed class SubsystemExplosivesPresentation : Subsystem, IUpdateable
{
    private readonly Dictionary<Point3, FuseParticleSystem> _fuses = new();

    private SubsystemExplosivesBlockBehavior _explosives = null!;

    private SubsystemTerrain _terrain = null!;

    private SubsystemParticles _particles = null!;

    private SubsystemAudio _audio = null!;

    private Sound? _fuseSound;

    private bool _disposed;

    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Presentation;

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public override void Load(ValuesDictionary valuesDictionary)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _explosives = Project.FindSubsystem<SubsystemExplosivesBlockBehavior>(true)!;
        _terrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _particles = Project.FindSubsystem<SubsystemParticles>(true)!;
        _audio = Project.FindSubsystem<SubsystemAudio>(true)!;
        _explosives.FuseRemoved += RemoveFuse;
        _terrain.Closing += OnTerrainClosing;
        _fuseSound = _audio.CreateSound("Audio/Fuse");
        _fuseSound.IsLooped = true;
    }

    public void Update(float dt)
    {
        if (_disposed || !WorldRuntimeContext.IsProjectActive(Project)) { return; }

        ReconcileFuseParticles();
        var distance = float.MaxValue;
        foreach (var point in _explosives.ActiveFusePoints)
        {
            distance = MathUtils.Min(distance, _audio.CalculateListenerDistance(new Vector3(point) + new Vector3(.5f)));
        }

        if (_fuseSound is null) { return; }

        _fuseSound.Volume = Configuration.ClientConfiguration.Preferences.SoundsVolume *
                            _audio.CalculateVolume(distance, 2f);
        if (_fuseSound.Volume > AudioManager.MinAudibleVolume) { _fuseSound.Play(); }
        else { _fuseSound.Pause(); }
    }

    internal void ReconcileFuseParticles()
    {
        if (_disposed) { return; }

        var active = _explosives.ActiveFusePoints;
        foreach (var point in _fuses.Keys.Where(point => !active.Contains(point)).ToArray()) { RemoveFuse(point); }

        foreach (var point in active)
        {
            if (_fuses.ContainsKey(point)) { continue; }

            var value = _terrain.Terrain.GetCellValue(point.X, point.Y, point.Z);
            if (_terrain.Catalog.Blocks[Terrain.ExtractContents(value)] is GunpowderKegBlock keg)
            {
                var particles = new FuseParticleSystem(new Vector3(point) + keg.FuseOffset);
                _particles.AddParticleSystem(particles);
                _fuses.Add(point, particles);
            }
            // The state baseline can arrive before terrain contents. Keep it and retry after installation.
        }
    }

    private void RemoveFuse(Point3 point)
    {
        if (_fuses.Remove(point, out var particles)) { _particles.RemoveParticleSystem(particles); }

        if (_explosives.ActiveFusePoints.Count == 0 && _fuseSound is { State: not SoundState.Disposed })
        {
            _fuseSound.Pause();
        }
    }

    private void OnTerrainClosing(SubsystemTerrain terrain) => Dispose();

    public override void Dispose()
    {
        if (_disposed) { return; }

        _disposed = true;
        if (_explosives is not null) { _explosives.FuseRemoved -= RemoveFuse; }

        if (_terrain is not null) { _terrain.Closing -= OnTerrainClosing; }

        foreach (var point in _fuses.Keys.ToArray()) { RemoveFuse(point); }

        _fuseSound?.Dispose();
        _fuseSound = null;
        base.Dispose();
    }
}
