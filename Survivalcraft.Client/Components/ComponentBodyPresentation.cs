using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public sealed class ComponentBodyPresentation : Component
{
    private ComponentBody _body = null!;

    private SubsystemAudio _subsystemAudio = null!;

    private SubsystemParticles _subsystemParticles = null!;

    private SubsystemTerrain _subsystemTerrain = null!;

    private readonly Random _random = new();

    private bool _fluidEffectsPlayed;

    private bool _subscribed;

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        if (!WorldRuntimeContext.For(Project).HasPresentation) { return; }

        _body = Entity.FindComponent<ComponentBody>(true)!;
        _subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true)!;
        _subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true)!;
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        Subscribe();
    }

    public override void OnEntityAdded() => Subscribe();
    public override void OnEntityRemoved() => Unsubscribe();
    public override void Dispose() => Unsubscribe();

    private void Subscribe()
    {
        if (_body is null || _subscribed) { return; }

        _body.ImmersionUpdated += OnImmersionUpdated;
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (_body is null || !_subscribed) { return; }

        _body.ImmersionUpdated -= OnImmersionUpdated;
        _subscribed = false;
    }

    private void OnImmersionUpdated(Vector3 position)
    {
        if (!_subscribed ||
            !WorldRuntimeContext.IsProjectActive(Project) ||
            !WorldRuntimeContext.For(Project).HasPresentation) { return; }

        switch (_body.ImmersionFluidBlock)
        {
            case WaterBlock when _body.ImmersionDepth > 0.3f && !_fluidEffectsPlayed:
            {
                _fluidEffectsPlayed = true;
                _subsystemAudio.PlayRandomSound(
                    "Audio/WaterFallIn",
                    _random.Float(0.75f, 1f),
                    _random.Float(-0.3f, 0f),
                    position,
                    4f,
                    true
                );
                _subsystemParticles.AddParticleSystem(
                    new WaterSplashParticleSystem(
                        _subsystemTerrain,
                        position,
                        (_body.BoundingBox.Max - _body.BoundingBox.Min).Length() > 0.8f
                    )
                );

                break;
            }
            case MagmaBlock when _body.ImmersionDepth > 0f && !_fluidEffectsPlayed:
            {
                _fluidEffectsPlayed = true;
                _subsystemAudio.PlaySound("Audio/SizzleLong", 1f, 0f, position, 4f, true);
                _subsystemParticles.AddParticleSystem(
                    new MagmaSplashParticleSystem(
                        _subsystemTerrain,
                        position,
                        (_body.BoundingBox.Max - _body.BoundingBox.Min).Length() > 0.8f
                    )
                );

                break;
            }
            case null:
                _fluidEffectsPlayed = false;
                break;
        }
    }
}
