using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems;

public sealed class SubsystemCampfirePresentation : Subsystem, IUpdateable
{
    private SubsystemCampfireBlockBehavior _campfires = null!;

    private SubsystemParticles _particles = null!;

    private SubsystemAmbientSounds _ambient = null!;

    private SubsystemTerrain _terrain = null!;

    private readonly Dictionary<Point3, int> _pending = [];

    private readonly Dictionary<Point3, FireParticleSystem> _active = [];

    private readonly HashSet<FireParticleSystem> _owned = [];

    private float _fireSoundVolume;

    private bool _disposed;

    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Presentation;

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public override void Load(ValuesDictionary valuesDictionary)
    {
        _campfires = Project.FindSubsystem<SubsystemCampfireBlockBehavior>(true)!;
        _particles = Project.FindSubsystem<SubsystemParticles>(true)!;
        _ambient = Project.FindSubsystem<SubsystemAmbientSounds>(true)!;
        _terrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _campfires.CampfireChanged += OnChanged;
        foreach (var point in _campfires.Campfires)
        {
            OnChanged(point, _terrain.Terrain.GetCellValue(point.X, point.Y, point.Z));
        }
    }

    private void OnChanged(Point3 point, int value) => _pending[point] = value;

    public void Update(float dt)
    {
        if (_disposed || !WorldRuntimeContext.IsProjectActive(Project)) { return; }

        foreach (var (point, value) in _pending)
        {
            if (_active.Remove(point, out var previous)) { previous.IsStopped = true; }

            var amount = Terrain.ExtractData(value);
            if (amount <= 0) { continue; }

            var system = new FireParticleSystem(
                new Vector3(point) + new Vector3(.5f, .15f, .5f),
                MathUtils.Lerp(.2f, .5f, amount / 15f),
                256f
            );
            _particles.AddParticleSystem(system);
            _active.Add(point, system);
            _owned.Add(system);
        }

        _pending.Clear();
        _owned.RemoveWhere(system => !_particles.ContainsParticleSystem(system));
        if (Time.PeriodicEvent(.5, 0.0))
        {
            var distance = float.MaxValue;
            foreach (var point in _campfires.Campfires)
            {
                distance = MathUtils.Min(
                    distance,
                    _ambient.SubsystemAudio.CalculateListenerDistanceSquared(new Vector3(point))
                );
            }

            _fireSoundVolume = _ambient.SubsystemAudio.CalculateVolume(MathUtils.Sqrt(distance), 2f);
        }

        _ambient.FireSoundVolume = MathUtils.Max(_ambient.FireSoundVolume, _fireSoundVolume);
    }

    public override void Dispose()
    {
        if (_disposed) { return; }

        _disposed = true;
        if (_campfires is not null) { _campfires.CampfireChanged -= OnChanged; }

        if (_particles is not null)
        {
            foreach (var system in _owned)
            {
                if (_particles.ContainsParticleSystem(system)) { _particles.RemoveParticleSystem(system); }
            }
        }

        _owned.Clear();
        _active.Clear();
        _pending.Clear();
    }
}
