using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public sealed class ComponentShapeshifterPresentation : Component, IUpdateable
{
    private ComponentBody _body = null!;

    private ComponentShapeshifter _shapeshifter = null!;

    private ComponentSpawn? _spawn;

    private SubsystemAudio _audio = null!;

    private SubsystemParticles _particles = null!;

    private ShapeshiftParticleSystem? _particleSystem;

    private bool _started;

    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Presentation;

    public UpdateOrder UpdateOrder => UpdateOrder.Default + 1;

    public void Update(float dt)
    {
        if (!IsAddedToProject ||
            !WorldRuntimeContext.For(Project).HasPresentation ||
            !_shapeshifter.ShapeshiftStartTime.HasValue) { return; }

        if (!_started)
        {
            _started = true;
            _particleSystem = new ShapeshiftParticleSystem();
            _particles.AddParticleSystem(_particleSystem);
            _audio.PlaySound("Audio/Shapeshift", 1f, 0f, _body.Position, 3f, true);
        }

        if (_particleSystem is not null) { _particleSystem.BoundingBox = _body.BoundingBox; }
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        if (!WorldRuntimeContext.For(Project).HasPresentation) { return; }

        _body = Entity.FindComponent<ComponentBody>(true)!;
        _shapeshifter = Entity.FindComponent<ComponentShapeshifter>(true)!;
        _spawn = Entity.FindComponent<ComponentSpawn>(true)!;
        _particles = Project.FindSubsystem<SubsystemParticles>(true)!;
        _audio = Project.FindSubsystem<SubsystemAudio>(true)!;
        _spawn.Despawned += OnDespawned;
    }

    private void OnDespawned(ComponentSpawn spawn) => StopParticles();

    public override void OnEntityRemoved() => StopParticles();

    public override void Dispose()
    {
        if (_spawn is not null) { _spawn.Despawned -= OnDespawned; }

        StopParticles();
    }

    private void StopParticles()
    {
        if (_particleSystem is not null)
        {
            _particleSystem.Stopped = true;
            _particleSystem = null;
        }
    }
}
