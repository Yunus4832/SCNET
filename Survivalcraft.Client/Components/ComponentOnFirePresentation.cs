using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public sealed class ComponentOnFirePresentation : Component, IUpdateable
{
    private ComponentBody _body = null!;

    private ComponentOnFire _fire = null!;

    private SubsystemAmbientSounds _ambientSounds = null!;

    private SubsystemAudio _audio = null!;

    private SubsystemParticles _particles = null!;

    private OnFireParticleSystem? _particleSystem;

    private float _soundVolume;

    private bool _wasOnFire;

    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Presentation;

    public UpdateOrder UpdateOrder => UpdateOrder.Default + 1;

    public void Update(float dt)
    {
        if (!IsAddedToProject || !WorldRuntimeContext.For(Project).HasPresentation) { return; }

        if (!_fire.IsOnFire)
        {
            if (_wasOnFire && _body is { ImmersionFactor: > 0.5f, ImmersionFluidBlock: WaterBlock })
            {
                _audio.PlaySound("Audio/SizzleLong", 1f, 0f, _body.BoundingBox.Center(), 4f, true);
            }

            _wasOnFire = false;
            StopParticles();
            return;
        }

        _wasOnFire = true;
        if (_particleSystem is null)
        {
            _particleSystem = new OnFireParticleSystem();
            _particles.AddParticleSystem(_particleSystem);
        }

        var bounds = _body.BoundingBox;
        _particleSystem.Position = bounds.Center();
        _particleSystem.Radius = 0.5f * MathUtils.Min(bounds.Max.X - bounds.Min.X, bounds.Max.Z - bounds.Min.Z);
        if (Time.PeriodicEvent(0.5, 0.0))
        {
            _soundVolume = _audio.CalculateVolume(_audio.CalculateListenerDistance(_body.Position), 2f, 5f);
        }

        _ambientSounds.FireSoundVolume = MathUtils.Max(_ambientSounds.FireSoundVolume, _soundVolume);
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        if (!WorldRuntimeContext.For(Project).HasPresentation) { return; }

        _body = Entity.FindComponent<ComponentBody>(true)!;
        _fire = Entity.FindComponent<ComponentOnFire>(true)!;
        _ambientSounds = Project.FindSubsystem<SubsystemAmbientSounds>(true)!;
        _audio = Project.FindSubsystem<SubsystemAudio>(true)!;
        _particles = Project.FindSubsystem<SubsystemParticles>(true)!;
    }

    public override void OnEntityRemoved() => StopParticles();

    public override void Dispose() => StopParticles();

    private void StopParticles()
    {
        if (_particleSystem is not null)
        {
            _particleSystem.IsStopped = true;
            _particleSystem = null;
        }

        _soundVolume = 0f;
    }
}
