using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public class ComponentFlu : Component, IUpdateable
{
    private ComponentCharacter _character = null!;

    private SubsystemGameInfo _info = null!;

    private SubsystemTime _time = null!;

    private readonly Random _random = new();

    private double _lastCoughTime = -1000;

    private double _lastEffectTime = -1000;

    public float CoughDuration;

    public float SneezeDuration;

    public float FluDuration;

    public float FluOnset;

    public float BlackoutDuration { get; private set; }

    public long EffectSequence { get; private set; }

    public ComponentFluPackage.EventType LastEffect { get; private set; }

    public bool LastWasCough { get; private set; }

    public bool HasFlu => FluDuration > 0f;

    public bool IsCoughing => CoughDuration > 0f;

    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Both;

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        var authority = WorldRuntimeContext.For(Project).IsAuthority;
        BlackoutDuration = MathUtils.Max(BlackoutDuration - dt, 0f);
        if (authority)
        {
            if (_info.WorldSettings.GameMode == GameMode.Creative ||
                !_info.WorldSettings.AreAdventureSurvivalMechanicsEnabled)
            {
                if (FluDuration > 0 || FluOnset > 0 || CoughDuration > 0 || SneezeDuration > 0 || BlackoutDuration > 0)
                {
                    FluDuration = FluOnset = CoughDuration = SneezeDuration = BlackoutDuration = 0;
                    Publish(ComponentFluPackage.EventType.SyncStat);
                }

                return;
            }

            if (HasFlu)
            {
                FluOnset = 0;
                var recovery = _character.ComponentVitalStats.Temperature switch
                {
                    > 16 => 2f, > 12 => 1.5f, < 8 => .5f, _ => 1f
                };
                FluDuration = MathUtils.Max(FluDuration - recovery * dt, 0);
                if (_character.ComponentHealth.Health > 0 &&
                    !_character.ComponentSleep.IsSleeping &&
                    _time.PeriodicGameTimeEvent(5, -.01) &&
                    _time.GameTime - _lastEffectTime > 13)
                {
                    FluEffect();
                }
            }
            else if (_character.ComponentVitalStats.Temperature < 6)
            {
                FluOnset += dt;
                if (FluOnset > 120 && _time.PeriodicGameTimeEvent(1, 0) && _random.Bool(.025f)) { StartFlu(); }

                var period = FluOnset > 120 ? 9 : 13;
                if (FluOnset > 60 && _time.PeriodicGameTimeEvent(period, -.01) && _random.Bool(.75f)) { Sneeze(); }
            }
            else { FluOnset = 0; }

            if (Time.PeriodicEvent(1, .5) &&
                _character.CharacterData.ControllerConnection is { IsConnected: true } owner)
            {
                NetworkSender.SendTo(
                    owner,
                    EntityStatePackageBuilder.Capture(this, ComponentFluPackage.EventType.SyncStat)
                );
            }
        }
        else { FluDuration = MathUtils.Max(FluDuration - dt, 0); }

        if ((CoughDuration > 0 || SneezeDuration > 0) &&
            _character.ComponentHealth.Health > 0 &&
            !_character.ComponentSleep.IsSleeping)
        {
            CoughDuration = MathUtils.Max(CoughDuration - dt, 0);
            SneezeDuration = MathUtils.Max(SneezeDuration - dt, 0);
            var angle = MathUtils.DegToRad(
                MathUtils.Lerp(
                    -35f,
                    -65f,
                    SimplexNoise.Noise(4f * (float)MathUtils.Remainder(_time.GameTime, 10000))
                )
            );
            _character.ComponentLocomotion.LookOrder = new Vector2(
                _character.ComponentLocomotion.LookOrder.X,
                MathUtils.Clamp(angle - _character.ComponentLocomotion.LookAngles.Y, -3f, 3f)
            );
            if (authority && _random.Bool(2f * dt))
            {
                _character.ComponentBody.ApplyImpulse(
                    -1.2f * _character.ComponentCreaturePose.EyeRotation.GetForwardVector()
                );
            }
        }
    }

    public void StartFlu()
    {
        if (!WorldRuntimeContext.For(Project).IsAuthority) { return; }

        if (!HasFlu) { _character.CharacterStats.TimesHadFlu++; }

        FluDuration = 900;
        _time.QueueGameTimeDelayedExecution(_time.GameTime + 10, () => _character.ComponentVitalStats.MakeSleepy(.2f));
        Publish(ComponentFluPackage.EventType.StartFlu);
    }

    public void Sneeze()
    {
        if (!WorldRuntimeContext.For(Project).IsAuthority) { return; }

        BeginSneeze();
        Publish(ComponentFluPackage.EventType.Sneeze);
    }

    private void BeginSneeze()
    {
        SneezeDuration = 1;
        LastWasCough = false;
        MakeNoise();
    }

    public void Cough()
    {
        if (!WorldRuntimeContext.For(Project).IsAuthority) { return; }

        BeginCough();
        Publish(ComponentFluPackage.EventType.Cough);
    }

    private void BeginCough()
    {
        _lastCoughTime = _time.GameTime;
        CoughDuration = 4;
        LastWasCough = true;
        MakeNoise();
    }

    private void MakeNoise()
    {
        Project.FindSubsystem<SubsystemNoise>(true)!.MakeNoise(_character.ComponentBody.Position, .25f, 10f);
    }

    public void FluEffect()
    {
        if (!WorldRuntimeContext.For(Project).IsAuthority) { return; }

        _lastEffectTime = _time.GameTime;
        BlackoutDuration = MathUtils.Lerp(4, 2, _character.ComponentHealth.Health);
        var injury = MathUtils.Min(.1f, _character.ComponentHealth.Health - .175f);
        if (injury > 0)
        {
            _time.QueueGameTimeDelayedExecution(
                _time.GameTime + .75,
                () =>
                    _character.ComponentHealth.Injure(
                        injury,
                        null,
                        false,
                        new LocalizedText(nameof(ComponentFlu), "4", "4")
                    )
            );
        }

        if (CoughDuration == 0 && (_time.GameTime - _lastCoughTime > 40 || _random.Bool(.5f))) { BeginCough(); }
        else if (SneezeDuration == 0) { BeginSneeze(); }

        Publish(ComponentFluPackage.EventType.FluEffect);
    }

    private void Publish(ComponentFluPackage.EventType type)
    {
        LastEffect = type;
        EffectSequence++;
        if (!IsAddedToProject) { return; }

        NetworkSender.SendToObservers(Entity, EntityStatePackageBuilder.Capture(this, type));
        if (_character.CharacterData.ControllerConnection is { IsConnected: true } owner)
        {
            NetworkSender.SendTo(owner, CharacterPackageBuilder.CaptureSurvivalStatistics(_character));
        }
    }

    public void ApplyState(ComponentFluPackage state)
    {
        if (WorldRuntimeContext.For(Project).IsAuthority || state.EffectSequence < EffectSequence) { return; }

        FluDuration = state.FluDuration;
        FluOnset = state.FluOnset;
        if (state.EffectSequence == EffectSequence) { return; }

        EffectSequence = state.EffectSequence;
        CoughDuration = state.CoughDuration;
        SneezeDuration = state.SneezeDuration;
        BlackoutDuration = state.BlackoutDuration;
        LastEffect = state.PackageEventType;
        LastWasCough = state.LastWasCough;
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        _info = Project.FindSubsystem<SubsystemGameInfo>(true)!;
        _time = Project.FindSubsystem<SubsystemTime>(true)!;
        _character = Entity.FindComponent<ComponentCharacter>(true)!;
        FluDuration = valuesDictionary.GetValue<float>("FluDuration");
        FluOnset = valuesDictionary.GetValue<float>("FluOnset");
        CoughDuration = valuesDictionary.GetValue("CoughDuration", 0f);
        SneezeDuration = valuesDictionary.GetValue("SneezeDuration", 0f);
        BlackoutDuration = valuesDictionary.GetValue("BlackoutDuration", 0f);
        EffectSequence = valuesDictionary.GetValue("EffectSequence", 0L);
        LastEffect = valuesDictionary.GetValue("LastEffect", ComponentFluPackage.EventType.SyncStat);
        LastWasCough = valuesDictionary.GetValue("LastWasCough", false);
    }

    public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
    {
        valuesDictionary.SetValue("FluDuration", FluDuration);
        valuesDictionary.SetValue("FluOnset", FluOnset);
        valuesDictionary.SetValue("CoughDuration", CoughDuration);
        valuesDictionary.SetValue("SneezeDuration", SneezeDuration);
        valuesDictionary.SetValue("BlackoutDuration", BlackoutDuration);
        valuesDictionary.SetValue("EffectSequence", EffectSequence);
        valuesDictionary.SetValue("LastEffect", LastEffect);
        valuesDictionary.SetValue("LastWasCough", LastWasCough);
    }
}
