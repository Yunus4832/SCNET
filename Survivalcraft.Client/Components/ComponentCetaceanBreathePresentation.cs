using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public sealed class ComponentCetaceanBreathePresentation : Component, IUpdateable
{
    private ComponentBody _body = null!;

    private SubsystemAudio _audio = null!;

    private SubsystemParticles _particles = null!;

    private SubsystemTerrain _terrain = null!;

    private WhalePlumeParticleSystem? _particleSystem;

    private CreatureWhalePlumePackage? _pendingPlume;

    private readonly Random _random = new();

    private bool _inactive;

    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Presentation;

    public UpdateOrder UpdateOrder => UpdateOrder.CreatureModels + 1;

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        if (!WorldRuntimeContext.For(Project).HasPresentation) { return; }

        _body = Entity.FindComponent<ComponentBody>(true)!;
        _audio = Project.FindSubsystem<SubsystemAudio>(true)!;
        _particles = Project.FindSubsystem<SubsystemParticles>(true)!;
        _terrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
    }

    public void ApplyPlume(CreatureWhalePlumePackage plume)
    {
        if (!CanPresent) { return; }

        StopPlume();
        _pendingPlume = plume.IsActive ? plume : null;
    }

    public void Update(float dt)
    {
        if (!IsAddedToProject || !CanPresent) { return; }

        if (_pendingPlume is { } plume)
        {
            _pendingPlume = null;
            _particleSystem = new WhalePlumeParticleSystem(_terrain, plume.Size, plume.Duration);
            _particles.AddParticleSystem(_particleSystem);
            _audio.PlayRandomSound(
                "Audio/Creatures/WhaleBlow",
                1f,
                _random.Float(-.2f, .2f),
                _body.Position,
                10f,
                true
            );
        }

        if (_particleSystem is null) { return; }

        _particleSystem.Position = _body.Position + new Vector3(0, .8f * _body.BoxSize.Y, 0);
        if (!_particles.ContainsParticleSystem(_particleSystem)) { _particleSystem = null; }
    }

    private bool CanPresent => !_inactive &&
                               WorldRuntimeContext.IsProjectActive(Project) &&
                               WorldRuntimeContext.For(Project).HasPresentation;

    private void StopPlume()
    {
        _particleSystem?.IsStopped = true;
        _particleSystem = null;
        _pendingPlume = null;
    }

    public override void OnEntityAdded() => _inactive = false;

    public override void OnEntityRemoved()
    {
        _inactive = true;
        StopPlume();
    }

    public override void Dispose()
    {
        _inactive = true;
        StopPlume();
    }
}
