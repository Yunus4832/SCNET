using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public class ComponentSickness : Component, IUpdateable
{
    private ComponentCharacter _componentCharacter = null!;

    private double? _lastNauseaTime;

    private double? _lastPukeTime;

    private double? _pukeEndTime;

    private SubsystemGameInfo _subsystemGameInfo = null!;

    private SubsystemTime _subsystemTime = null!;

    public const string Name = "ComponentSickness";

    public float SicknessDuration;

    public long EffectSequence { get; private set; }

    public float NauseaDuration { get; private set; }

    public bool IsNausea { get; private set; }

    public float PukeDuration => _pukeEndTime is { } end
        ? (float)Math.Max(0, end - _subsystemTime.GameTime)
        : 0f;

    public bool IsSick => SicknessDuration > 0f;

    public bool IsPuking => PukeDuration > 0f;

    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Both;

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        NauseaDuration = MathUtils.Max(NauseaDuration - dt, 0f);
        var authority = WorldRuntimeContext.For(Project).IsAuthority;
        if (authority)
        {
            if (_subsystemGameInfo.WorldSettings.GameMode == GameMode.Creative ||
                !_subsystemGameInfo.WorldSettings.AreAdventureSurvivalMechanicsEnabled)
            {
                if (IsSick || IsPuking || NauseaDuration > 0f) { EndSickness(); }

                return;
            }

            if (SicknessDuration > 0f)
            {
                var recovery = _componentCharacter.ComponentSleep is
                    { IsSleeping: true, SubsystemUpdate.UpdatesPerFrame: 1 }
                    ? _subsystemGameInfo.WorldSettings.RecoverFactor
                    : 1f;
                SicknessDuration = MathUtils.Max(SicknessDuration - dt * recovery, 0f);
                if (_componentCharacter.ComponentHealth.Health > 0f &&
                    !_componentCharacter.ComponentSleep.IsSleeping &&
                    _subsystemTime.PeriodicGameTimeEvent(3.0, -0.01) &&
                    (!_lastNauseaTime.HasValue || _subsystemTime.GameTime - _lastNauseaTime > 15.0))
                {
                    NauseaEffect();
                }
            }
        }
        else { SicknessDuration = MathUtils.Max(SicknessDuration - dt, 0f); }

        if (IsPuking)
        {
            var angle = MathUtils.DegToRad(
                MathUtils.Lerp(
                    -35f,
                    -60f,
                    SimplexNoise.Noise(2f * (float)MathUtils.Remainder(_subsystemTime.GameTime, 10000.0))
                )
            );
            _componentCharacter.ComponentLocomotion.LookOrder = new Vector2(
                _componentCharacter.ComponentLocomotion.LookOrder.X,
                MathUtils.Clamp(angle - _componentCharacter.ComponentLocomotion.LookAngles.Y, -2f, 2f)
            );
        }
    }

    public void StartSickness()
    {
        if (!WorldRuntimeContext.For(Project).IsAuthority) { return; }

        if (!IsSick) { _componentCharacter.CharacterStats.TimesWasSick++; }

        SicknessDuration = 900f;
        PublishState();
    }

    public void EndSickness()
    {
        if (!WorldRuntimeContext.For(Project).IsAuthority) { return; }

        SicknessDuration = 0f;
        NauseaDuration = 0f;
        _pukeEndTime = null;
        IsNausea = false;
        EffectSequence++;
        PublishState();
    }

    public void Puke()
    {
        if (!WorldRuntimeContext.For(Project).IsAuthority) { return; }

        BeginPuke();
        IsNausea = false;
        EffectSequence++;
        PublishState();
    }

    private void BeginPuke()
    {
        _lastPukeTime = _subsystemTime.GameTime;
        _pukeEndTime = _subsystemTime.GameTime + 3.5;
        NauseaDuration = .8f;
        _componentCharacter.CharacterStats.TimesPuked++;
        Project.FindSubsystem<SubsystemNoise>(true)!
            .MakeNoise(_componentCharacter.ComponentBody.Position, .25f, 10f);
    }

    public void NauseaEffect()
    {
        if (!WorldRuntimeContext.For(Project).IsAuthority) { return; }

        _lastNauseaTime = _subsystemTime.GameTime;
        var injury = MathUtils.Min(.1f, _componentCharacter.ComponentHealth.Health - .075f);
        if (injury > 0f)
        {
            _subsystemTime.QueueGameTimeDelayedExecution(
                _subsystemTime.GameTime + .75,
                () =>
                    _componentCharacter.ComponentHealth.Injure(injury, null, false, new LocalizedText(Name, "1", "1"))
            );
        }

        if (!IsPuking && (!_lastPukeTime.HasValue || _subsystemTime.GameTime - _lastPukeTime > 50))
        {
            BeginPuke();
        }
        else { NauseaDuration = MathUtils.Lerp(4f, 2f, _componentCharacter.ComponentHealth.Health); }

        IsNausea = true;
        EffectSequence++;
        PublishState();
    }

    public void ApplyState(float duration, long sequence, float pukeDuration, float nauseaDuration, bool isNausea)
    {
        if (WorldRuntimeContext.For(Project).IsAuthority || sequence < EffectSequence) { return; }

        SicknessDuration = duration;
        if (sequence == EffectSequence) { return; }

        EffectSequence = sequence;
        _pukeEndTime = pukeDuration > 0 ? _subsystemTime.GameTime + pukeDuration : null;
        NauseaDuration = nauseaDuration;
        IsNausea = isNausea;
    }

    private void PublishState()
    {
        if (!IsAddedToProject) { return; }

        NetworkSender.SendToObservers(Entity, EntityStatePackageBuilder.Capture(this));
        if (_componentCharacter.CharacterData.ControllerConnection is { IsConnected: true } owner)
        {
            NetworkSender.SendTo(owner, CharacterPackageBuilder.CaptureSurvivalStatistics(_componentCharacter));
        }
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        _subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true)!;
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _componentCharacter = Entity.FindComponent<ComponentCharacter>(true)!;
        SicknessDuration = valuesDictionary.GetValue<float>("SicknessDuration");
        EffectSequence = valuesDictionary.GetValue("EffectSequence", 0L);
        NauseaDuration = valuesDictionary.GetValue("NauseaDuration", 0f);
        IsNausea = valuesDictionary.GetValue("IsNausea", false);
        var pukeDuration = valuesDictionary.GetValue("PukeDuration", 0f);
        _pukeEndTime = pukeDuration > 0 ? _subsystemTime.GameTime + pukeDuration : null;
    }

    public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
    {
        valuesDictionary.SetValue("SicknessDuration", SicknessDuration);
        valuesDictionary.SetValue("EffectSequence", EffectSequence);
        valuesDictionary.SetValue("NauseaDuration", NauseaDuration);
        valuesDictionary.SetValue("IsNausea", IsNausea);
        valuesDictionary.SetValue("PukeDuration", PukeDuration);
    }
}
