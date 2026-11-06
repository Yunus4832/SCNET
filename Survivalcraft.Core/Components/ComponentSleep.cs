using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public class ComponentSleep : Component, IUpdateable
{
    private const string _typeName = nameof(ComponentSleep);

    private ComponentCharacter _character = null!;

    private float _minWetness;

    private float _sleepFactor;

    private SubsystemGameInfo _info = null!;

    private SubsystemTerrain _terrain = null!;

    private SubsystemTimeOfDay _timeOfDay = null!;

    public SubsystemUpdate SubsystemUpdate = null!;

    public double? SleepStartTime { get; private set; }

    public bool AllowManualWakeUp { get; private set; }

    public SleepWakeReason LastWakeReason { get; private set; }

    public bool IsSleeping => SleepStartTime.HasValue;

    public float SleepFactor => _sleepFactor;

    public bool CanManuallyWakeUp => IsSleeping &&
                                     AllowManualWakeUp &&
                                     _info.TotalElapsedGameTime - SleepStartTime!.Value > 10;

    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Both;

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        var authority = WorldRuntimeContext.For(Project).IsAuthority;
        if (IsSleeping && _character.ComponentHealth.Health > 0)
        {
            var recovery = authority && SubsystemUpdate.UpdatesPerFrame == 1 ? _info.WorldSettings.RecoverFactor : 1f;
            _sleepFactor = MathUtils.Min(_sleepFactor + .33f * dt * recovery, 1);
            if (!authority) { return; }

            _minWetness = MathUtils.Min(_minWetness, _character.ComponentVitalStats.Wetness);
            _character.CharacterStats.TimeSlept += _info.TotalElapsedGameTimeDelta;
            if ((_character.ComponentVitalStats.Sleep >= 1 || _info.WorldSettings.GameMode == GameMode.Creative) &&
                _timeOfDay.TimeOfDay is > .3f and < .6f &&
                _info.TotalElapsedGameTime > SleepStartTime + 180)
            {
                WakeUp();
            }
            else if (_character.ComponentHealth.HealthChange < 0 &&
                     (_character.ComponentHealth.Health < .5f || _character.ComponentVitalStats.Sleep > .5f))
            {
                WakeUp(SleepWakeReason.Damage);
            }
            else if (_character.ComponentVitalStats.Wetness > _minWetness + .05f &&
                     _character.ComponentVitalStats.Sleep > .2f)
            {
                WakeUp(SleepWakeReason.Wet);
            }

            if (IsSleeping && Time.PeriodicEvent(1, .5)) { SendStatistics(); }
        }
        else
        {
            _sleepFactor = MathUtils.Max(_sleepFactor - dt, 0);
            if (authority && IsSleeping) { WakeUp(); }
        }
    }

    public bool CanSleep(out LocalizedText reason)
    {
        var block = _character.ComponentBody.StandingOnValue.HasValue
            ? WorldContentBinding.For(Project).Catalog
                .Blocks[Terrain.ExtractContents(_character.ComponentBody.StandingOnValue.Value)]
            : null;

        if (block is null || _character.ComponentBody.ImmersionDepth > 0f)
        {
            reason = new LocalizedText(_typeName, "1", "1");
            return false;
        }

        if (block.SleepSuitability == 0f)
        {
            reason = new LocalizedText(_typeName, "2", "2");
            return false;
        }

        if (_character.ComponentVitalStats.Sleep > 0.99f)
        {
            reason = new LocalizedText(_typeName, "3", "3");
            return false;
        }

        if (_character.ComponentVitalStats.Wetness > 0.95f)
        {
            reason = new LocalizedText(_typeName, "4", "4");
            return false;
        }

        for (var i = -1; i <= 1; i++)
        {
            for (var j = -1; j <= 1; j++)
            {
                var start = _character.ComponentBody.Position + new Vector3(i, 1f, j);
                var end = new Vector3(start.X, 255f, start.Z);
                if (_terrain
                    .Raycast(start, end, false, true, (value, _) => Terrain.ExtractContents(value) != 0)
                    .HasValue)
                {
                    continue;
                }

                reason = new LocalizedText(_typeName, "5", "5");
                return false;
            }
        }

        reason = LocalizedText.Empty;
        return true;
    }

    public void Sleep(bool allowManualWakeup)
    {
        if (!WorldRuntimeContext.For(Project).IsAuthority || IsSleeping) { return; }

        SleepStartTime = _info.TotalElapsedGameTime;
        AllowManualWakeUp = allowManualWakeup;
        _minWetness = float.MaxValue;
        _character.CharacterStats.TimesWentToSleep++;
        if (IsAddedToProject)
        {
            NetworkSender.SendToObservers(
                Entity,
                new SleepResultPackage
                {
                    EntityId = Entity.EntityId, AllowManualWakeup = AllowManualWakeUp, Result = true,
                    StartTime = SleepStartTime.Value
                }
            );
            SendStatistics();
        }
    }

    public void WakeUp(SleepWakeReason reason = SleepWakeReason.Automatic)
    {
        if (!WorldRuntimeContext.For(Project).IsAuthority || !IsSleeping) { return; }

        SleepStartTime = null;
        LastWakeReason = reason;
        _character.CharacterData.SpawnPosition = _character.ComponentBody.Position + new Vector3(0, .1f, 0);
        if (IsAddedToProject)
        {
            NetworkSender.SendToObservers(Entity, new WakeupPackage { EntityId = Entity.EntityId, Reason = reason });
            SendStatistics();
        }
    }

    public void RequestWakeUp()
    {
        if (WorldRuntimeContext.For(Project).IsAuthority || !CanManuallyWakeUp) { return; }

        NetworkSender.SendToServer(
            WorldRuntimeContext.For(Project),
            new WakeupRequestPackage { EntityId = Entity.EntityId, SleepStartTime = SleepStartTime!.Value }
        );
    }

    public bool AcceptsWakeUpRequest(double startTime)
    {
        return WorldRuntimeContext.For(Project).IsAuthority && SleepStartTime == startTime && CanManuallyWakeUp;
    }

    public void ApplySleepState(
        double? startTime,
        bool allowManualWakeup,
        SleepWakeReason reason = SleepWakeReason.Automatic
    )
    {
        if (WorldRuntimeContext.For(Project).IsAuthority) { return; }

        SleepStartTime = startTime;
        AllowManualWakeUp = allowManualWakeup;
        LastWakeReason = reason;
    }

    private void SendStatistics()
    {
        if (_character.CharacterData.ControllerConnection is { IsConnected: true } owner)
        {
            NetworkSender.SendTo(owner, CharacterPackageBuilder.CaptureSurvivalStatistics(_character));
        }
    }

    private void OnAttacked(ComponentCreature attacker)
    {
        if (WorldRuntimeContext.For(Project).IsAuthority && IsSleeping && _character.ComponentVitalStats.Sleep > .25f)
        {
            WakeUp();
        }
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        SubsystemUpdate = Project.FindSubsystem<SubsystemUpdate>(true)!;
        _info = Project.FindSubsystem<SubsystemGameInfo>(true)!;
        _timeOfDay = Project.FindSubsystem<SubsystemTimeOfDay>(true)!;
        _terrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _character = Entity.FindComponent<ComponentCharacter>(true)!;
        var startTime = valuesDictionary.GetValue<double>("SleepStartTime");
        SleepStartTime = startTime > 0 ? startTime : null;
        AllowManualWakeUp = valuesDictionary.GetValue<bool>("AllowManualWakeUp");
        _sleepFactor = IsSleeping ? 1 : 0;
        _minWetness = float.MaxValue;
        _character.ComponentHealth.Attacked += OnAttacked;
    }

    public override void Dispose()
    {
        if (_character is not null) { _character.ComponentHealth.Attacked -= OnAttacked; }
    }

    public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
    {
        valuesDictionary.SetValue("SleepStartTime", SleepStartTime ?? 0);
        valuesDictionary.SetValue("AllowManualWakeUp", AllowManualWakeUp);
    }
}
