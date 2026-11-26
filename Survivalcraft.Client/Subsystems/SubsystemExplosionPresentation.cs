using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems;

public sealed class SubsystemExplosionPresentation : Subsystem
{
    private SubsystemParticles _particles = null!;

    private SubsystemSpatialSoundRequests _sounds = null!;

    private ExplosionParticleSystem? _system;

    private bool _disposed;

    public override void Load(ValuesDictionary valuesDictionary)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _particles = Project.FindSubsystem<SubsystemParticles>(true)!;
        _sounds = Project.FindSubsystem<SubsystemSpatialSoundRequests>(true)!;
    }

    public void ApplyCells(IEnumerable<ExplosionCell> cells)
    {
        if (_disposed ||
            !WorldRuntimeContext.IsProjectActive(Project) ||
            !WorldRuntimeContext.For(Project).HasPresentation) { return; }

        foreach (var cell in cells)
        {
            if (_system is null)
            {
                _system = new ExplosionParticleSystem();
                _particles.AddParticleSystem(_system);
            }

            _system.SetExplosionCell(cell.Position, cell.Pressure);
        }
    }

    public void PlaySound(Vector3 position, float level, float pitch)
    {
        if (_disposed ||
            !WorldRuntimeContext.IsProjectActive(Project) ||
            !WorldRuntimeContext.For(Project).HasPresentation) { return; }

        var profile = ExplosionAcoustics.ForPressure(level);
        if (profile.MinimumDistance <= 0f) { return; }

        _sounds.RequestSound(profile.Asset, 1f, pitch, position, profile.MinimumDistance, true);
    }

    public override void Dispose()
    {
        if (_disposed) { return; }

        _disposed = true;
        if (_system is not null) { _particles.RemoveParticleSystem(_system); }

        _system = null;
    }
}
