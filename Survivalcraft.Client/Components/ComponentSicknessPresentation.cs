using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public sealed class ComponentSicknessPresentation : Component, IUpdateable
{
    private ComponentCharacter _character = null!;

    private ComponentSickness _sickness = null!;

    private SubsystemParticles _particles = null!;

    private SubsystemTerrain _terrain = null!;

    private SubsystemTime _time = null!;

    private PukeParticleSystem? _puke;

    private float _greenout;

    private long _lastSequence;

    private bool _inactive;

    private double _lastMessageTime = double.NegativeInfinity;

    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Presentation;

    public UpdateOrder UpdateOrder => UpdateOrder.CreatureModels + 1;

    public void Update(float dt)
    {
        if (_inactive || !IsAddedToProject || !WorldRuntimeContext.For(Project).HasPresentation) { return; }

        if (_sickness.EffectSequence > _lastSequence)
        {
            _lastSequence = _sickness.EffectSequence;
            if (_sickness.IsNausea && _sickness.NauseaDuration > 0)
            {
                _character.Entity.FindComponent<ComponentCreatureSoundsPresentation>(true)!.PlayMoanSound();
                if (!_sickness.IsPuking && _character.IsLocallyControlled && _time.GameTime - _lastMessageTime > 60)
                {
                    _lastMessageTime = _time.GameTime;
                    var sequence = _lastSequence;
                    _time.QueueGameTimeDelayedExecution(_time.GameTime + 1.5, () => ShowNauseaMessage(sequence));
                }
            }

            if (_sickness.IsPuking)
            {
                _character.Entity.FindComponent<ComponentCreatureSoundsPresentation>(true)!.PlayPukeSound();
            }
        }

        if (_sickness.IsPuking)
        {
            if (_puke is null)
            {
                _puke = new PukeParticleSystem(_terrain);
                _particles.AddParticleSystem(_puke);
            }

            var model = _character.ComponentCreaturePose;
            var up = model.EyeRotation.GetUpVector();
            var forward = model.EyeRotation.GetForwardVector();
            _puke.Position = model.EyePosition - .08f * up + .3f * forward;
            _puke.Direction = Vector3.Normalize(forward + .5f * up);
        }
        else { StopPuke(); }

        _greenout = _sickness.NauseaDuration > 0f
            ? MathUtils.Min(_greenout + .5f * dt, .95f)
            : MathUtils.Max(_greenout - .5f * dt, 0f);
        if (_character.IsLocallyControlled)
        {
            _character.Entity.FindComponent<ComponentScreenOverlays>(true)!.GreenOutFactor = MathUtils.Max(
                _greenout,
                _character.Entity.FindComponent<ComponentScreenOverlays>(true)!.GreenOutFactor
            );
        }
    }

    private void ShowNauseaMessage(long sequence)
    {
        if (!_inactive &&
            IsAddedToProject &&
            WorldRuntimeContext.IsProjectActive(Project) &&
            _sickness.EffectSequence == sequence &&
            _sickness.NauseaDuration > 0 &&
            _character.IsLocallyControlled)
        {
            _character.Entity.FindComponent<ComponentGui>(true)!.DisplaySmallMessage(
                Localization.ClientLanguage.Catalog.Get(ComponentSickness.Name, 2),
                Color.White,
                true,
                true
            );
        }
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        if (!WorldRuntimeContext.For(Project).HasPresentation) { return; }

        _character = Entity.FindComponent<ComponentCharacter>(true)!;
        _sickness = Entity.FindComponent<ComponentSickness>(true)!;
        _particles = Project.FindSubsystem<SubsystemParticles>(true)!;
        _terrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _time = Project.FindSubsystem<SubsystemTime>(true)!;
        _lastSequence = _sickness.EffectSequence;
    }

    public override void OnEntityAdded() => _inactive = false;

    public override void OnEntityRemoved()
    {
        _inactive = true;
        StopPuke();
    }

    public override void Dispose()
    {
        _inactive = true;
        StopPuke();
    }

    private void StopPuke()
    {
        if (_puke is not null)
        {
            _puke.IsStopped = true;
            _puke = null;
        }
    }
}
