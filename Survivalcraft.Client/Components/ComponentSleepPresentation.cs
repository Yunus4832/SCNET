using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public sealed class ComponentSleepPresentation : Component, IUpdateable
{
    private ComponentCharacter _character = null!;

    private ComponentSleep _sleep = null!;

    private SubsystemGameInfo _info = null!;

    private SubsystemTime _time = null!;

    private double? _lastStartTime;

    private long _episode;

    private bool _inactive;

    private bool _wakeRequested;

    private double _nextWakeRequest;

    private float _factor;

    private float _messageFactor;

    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Presentation;

    public UpdateOrder UpdateOrder => UpdateOrder.CreatureModels + 1;

    public void Update(float dt)
    {
        if (_inactive ||
            !IsAddedToProject ||
            !WorldRuntimeContext.For(Project).HasPresentation ||
            !_character.IsLocallyControlled) { return; }

        if (_sleep.SleepStartTime != _lastStartTime)
        {
            _episode++;
            _wakeRequested = false;
            _messageFactor = 0;
            if (_lastStartTime.HasValue &&
                !_sleep.IsSleeping &&
                _sleep.LastWakeReason is SleepWakeReason.Manual or SleepWakeReason.Wet)
            {
                var episode = _episode;
                var key = _sleep.LastWakeReason == SleepWakeReason.Wet ? 6 : 7;
                _time.QueueGameTimeDelayedExecution(
                    _time.GameTime + (key == 6 ? 1 : 2),
                    () => ShowWakeMessage(episode, key)
                );
            }

            _lastStartTime = _sleep.SleepStartTime;
        }

        _factor = _sleep.IsSleeping ? MathUtils.Min(_factor + .33f * dt, 1) : MathUtils.Max(_factor - dt, 0);
        var overlays = _character.Entity.FindComponent<ComponentScreenOverlays>(true)!;
        if (_sleep.IsSleeping)
        {
            var elapsed = _info.TotalElapsedGameTime - _sleep.SleepStartTime!.Value;
            if (_sleep.CanManuallyWakeUp)
            {
                if (_character.Entity.FindComponent<ComponentCharacterPresentation>(true)!.GameWidget.Input.Any &&
                    !DialogsManager.HasDialogs(
                        _character.Entity.FindComponent<ComponentCharacterPresentation>(true)!.GameWidget
                    ))
                {
                    _character.Entity.FindComponent<ComponentCharacterPresentation>(true)!.GameWidget.Input.Clear();
                    _wakeRequested = true;
                }

                if (_wakeRequested && _time.GameTime >= _nextWakeRequest)
                {
                    _sleep.RequestWakeUp();
                    _nextWakeRequest = _time.GameTime + .5;
                }

                _messageFactor = MathUtils.Min(_messageFactor + .5f * dt, 1);
                overlays.Message = Localization.ClientLanguage.Catalog.Get(nameof(ComponentSleep), 8);
                overlays.MessageFactor = _messageFactor;
            }
            else if (!_sleep.AllowManualWakeUp && elapsed > 5)
            {
                _messageFactor = MathUtils.Min(_messageFactor + dt, 1);
                overlays.Message = Localization.ClientLanguage.Catalog.Get(nameof(ComponentSleep), 9);
                overlays.MessageFactor = _messageFactor;
            }
        }

        overlays.BlackoutFactor = MathUtils.Max(overlays.BlackoutFactor, _factor);
        if (_factor > .01f)
        {
            overlays.FloatingMessage = Localization.ClientLanguage.Catalog.Get(nameof(ComponentSleep), 10);
            overlays.FloatingMessageFactor = MathUtils.Saturate(10f * (_factor - .9f));
        }
    }

    private void ShowWakeMessage(long episode, int key)
    {
        if (!_inactive &&
            IsAddedToProject &&
            WorldRuntimeContext.IsProjectActive(Project) &&
            _episode == episode &&
            _character.IsLocallyControlled)
        {
            _character.Entity.FindComponent<ComponentGui>(true)!.DisplaySmallMessage(
                Localization.ClientLanguage.Catalog.Get(nameof(ComponentSleep), key),
                Color.White,
                true,
                key == 6
            );
        }
    }

    internal void ShowFailure(LocalizedText reason)
    {
        if (WorldRuntimeContext.For(Project).HasPresentation && _character.IsLocallyControlled)
        {
            _character.Entity.FindComponent<ComponentGui>(true)!.DisplaySmallMessage(
                Localization.ClientLanguage.Resolver.Resolve(reason),
                Color.White,
                false,
                true
            );
        }
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        if (!WorldRuntimeContext.For(Project).HasPresentation) { return; }

        _character = Entity.FindComponent<ComponentCharacter>(true)!;
        _sleep = Entity.FindComponent<ComponentSleep>(true)!;
        _info = Project.FindSubsystem<SubsystemGameInfo>(true)!;
        _time = Project.FindSubsystem<SubsystemTime>(true)!;
        _lastStartTime = _sleep.SleepStartTime;
        _factor = _sleep.IsSleeping ? 1 : 0;
    }

    public override void OnEntityAdded() => _inactive = false;

    public override void OnEntityRemoved() => _inactive = true;

    public override void Dispose() => _inactive = true;
}
