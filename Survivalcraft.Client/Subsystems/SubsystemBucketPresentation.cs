using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems;

public sealed class SubsystemBucketPresentation : Subsystem
{
    private readonly Func<SubsystemTerrain, Vector3, Vector3, Color, PaintParticleSystem> _createPaint;

    private readonly HashSet<PaintParticleSystem> _owned = [];

    private SubsystemBucketBlockBehavior _buckets = null!;

    private SubsystemTerrain _terrain = null!;

    private SubsystemPalette _palette = null!;

    private SubsystemParticles _particles = null!;

    private bool _disposed;

    public SubsystemBucketPresentation() : this((terrain, position, normal, color) =>
        new PaintParticleSystem(terrain, position, normal, color)
    )
    {
    }

    internal SubsystemBucketPresentation(
        Func<SubsystemTerrain, Vector3, Vector3, Color, PaintParticleSystem> createPaint
    ) => _createPaint = createPaint;

    public override void Load(ValuesDictionary valuesDictionary)
    {
        _buckets = Project.FindSubsystem<SubsystemBucketBlockBehavior>(true)!;
        _terrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _palette = Project.FindSubsystem<SubsystemPalette>(true)!;
        _particles = Project.FindSubsystem<SubsystemParticles>(true)!;
        _buckets.PaintEffectRequested += OnPaintEffectRequested;
        _terrain.Closing += OnTerrainClosing;
    }

    private void OnPaintEffectRequested(SubsystemBucketBlockBehavior source, PaintEffectRequest effect)
    {
        if (_disposed ||
            !ReferenceEquals(source, _buckets) ||
            !ReferenceEquals(source.Project, Project) ||
            !WorldRuntimeContext.IsProjectActive(Project)) { return; }

        _owned.RemoveWhere(system => !_particles.ContainsParticleSystem(system));
        var color = effect.ColorIndex.HasValue
            ? _palette.GetColor(effect.ColorIndex.Value)
            : new Color(128, 128, 128, 128);
        var system = _createPaint(_terrain, effect.Position, effect.Normal, color);
        _particles.AddParticleSystem(system);
        _owned.Add(system);
    }

    private void OnTerrainClosing(SubsystemTerrain terrain) => Dispose();

    public override void Dispose()
    {
        if (_disposed) { return; }

        _disposed = true;
        if (_buckets is not null) { _buckets.PaintEffectRequested -= OnPaintEffectRequested; }

        if (_terrain is not null) { _terrain.Closing -= OnTerrainClosing; }

        foreach (var system in _owned)
        {
            if (_particles.ContainsParticleSystem(system)) { _particles.RemoveParticleSystem(system); }
        }

        _owned.Clear();
    }
}
