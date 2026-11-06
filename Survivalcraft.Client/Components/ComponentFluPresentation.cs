using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public sealed class ComponentFluPresentation : Component, IUpdateable
{
    private ComponentCharacter _character = null!;

    private ComponentFlu _flu = null!;

    private SubsystemTime _time = null!;

    private long _lastSequence;

    private double _lastMessageTime = double.NegativeInfinity;

    private float _blackout;

    private bool _inactive;

    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Presentation;

    public UpdateOrder UpdateOrder => UpdateOrder.CreatureModels + 1;

    public void Update(float dt)
    {
        if (_inactive || !IsAddedToProject || !WorldRuntimeContext.For(Project).HasPresentation) { return; }

        if (_flu.EffectSequence > _lastSequence)
        {
            _lastSequence = _flu.EffectSequence;
            if (_flu.LastEffect is ComponentFluPackage.EventType.FluEffect or ComponentFluPackage.EventType.Cough
                or ComponentFluPackage.EventType.Sneeze)
            {
                if (_flu.LastWasCough)
                {
                    _character.Entity.FindComponent<ComponentCreatureSoundsPresentation>(true)!.PlayCoughSound();
                }
                else { _character.Entity.FindComponent<ComponentCreatureSoundsPresentation>(true)!.PlaySneezeSound(); }
            }

            if (_flu.LastEffect == ComponentFluPackage.EventType.FluEffect &&
                _character.IsLocallyControlled &&
                _time.GameTime - _lastMessageTime > 60)
            {
                _lastMessageTime = _time.GameTime;
                var sequence = _lastSequence;
                _time.QueueGameTimeDelayedExecution(_time.GameTime + 1.5, () => ShowFluMessage(sequence));
            }
        }

        _blackout = _flu.BlackoutDuration > 0
            ? MathUtils.Min(_blackout + .5f * dt, .95f)
            : MathUtils.Max(_blackout - .5f * dt, 0);
        if (!_character.IsLocallyControlled) { return; }

        _character.Entity.FindComponent<ComponentScreenOverlays>(true)!.BlackoutFactor = MathUtils.Max(
            _blackout,
            _character.Entity.FindComponent<ComponentScreenOverlays>(true)!.BlackoutFactor
        );
        if (_flu.FluOnset > 120 && _time.GameTime - _lastMessageTime > 60)
        {
            _lastMessageTime = _time.GameTime;
            _character.Entity.FindComponent<ComponentGui>(true)!.DisplaySmallMessage(
                Localization.ClientLanguage.Catalog.Get(nameof(ComponentFlu), 1),
                Color.White,
                true,
                true
            );
        }
    }

    private void ShowFluMessage(long sequence)
    {
        if (_inactive ||
            !IsAddedToProject ||
            !WorldRuntimeContext.IsProjectActive(Project) ||
            _flu.EffectSequence != sequence ||
            !_character.IsLocallyControlled) { return; }

        _character.Entity.FindComponent<ComponentGui>(true)!.DisplaySmallMessage(
            Localization.ClientLanguage.Catalog.Get(
                nameof(ComponentFlu),
                _character.ComponentVitalStats.Temperature < 8 ? 2 : 3
            ),
            Color.White,
            true,
            true
        );
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        if (!WorldRuntimeContext.For(Project).HasPresentation) { return; }

        _character = Entity.FindComponent<ComponentCharacter>(true)!;
        _flu = Entity.FindComponent<ComponentFlu>(true)!;
        _time = Project.FindSubsystem<SubsystemTime>(true)!;
        _lastSequence = _flu.EffectSequence;
    }

    public override void OnEntityAdded() => _inactive = false;

    public override void OnEntityRemoved() => _inactive = true;

    public override void Dispose() => _inactive = true;
}
