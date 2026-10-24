using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Network;
using Game.Network.Transport;

namespace Game.Components;

public class ComponentSleep : Component, IUpdateable
{
    private const string _typeName = nameof(ComponentSleep);

    private bool _allowManualWakeUp;

    private ComponentCharacter _componentCharacter = null!;

    private float _messageFactor;

    private float _minWetness;

    private float _sleepFactor;

    private double? _sleepStartTime;

    private SubsystemGameInfo _subsystemGameInfo = null!;

    private SubsystemCharacters _subsystemCharacters = null!;

    private SubsystemTerrain _subsystemTerrain = null!;

    private SubsystemTime _subsystemTime = null!;

    private SubsystemTimeOfDay _subsystemTimeOfDay = null!;

    public SubsystemUpdate SubsystemUpdate = null!;

    public bool IsSleeping => _sleepStartTime.HasValue;

    public float SleepFactor => _sleepFactor;

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        var runGui = RunMode.Value is RunModeType.Gui;
        if (IsSleeping && _componentCharacter.ComponentHealth.Health > 0f)
        {
            //开服后，睡觉恢复速度加快5倍
            var amount = 0.33f * Time.FrameDuration;
            if (CommonLib.WorkType == WorkType.Server &&
                _componentCharacter.ComponentSleep.SubsystemUpdate.UpdatesPerFrame == 1 &&
                _componentCharacter.ComponentSleep.IsSleeping)
            {
                amount *= _subsystemGameInfo.WorldSettings.RecoverFactor;
            }

            _sleepFactor = MathUtils.Min(_sleepFactor + amount, 1f);
            _minWetness = MathUtils.Min(_minWetness, _componentCharacter.ComponentVitalStats.Wetness);
            _componentCharacter.CharacterStats.TimeSlept += _subsystemGameInfo.TotalElapsedGameTimeDelta;
            if ((_componentCharacter.ComponentVitalStats.Sleep >= 1f ||
                 _subsystemGameInfo.WorldSettings.GameMode == GameMode.Creative) &&
                _subsystemTimeOfDay.TimeOfDay > 0.3f &&
                _subsystemTimeOfDay.TimeOfDay < 0.599999964f &&
                _sleepStartTime.HasValue &&
                _subsystemGameInfo.TotalElapsedGameTime > _sleepStartTime + 180.0)
            {
                WakeUp();
            }

            if (_componentCharacter.ComponentHealth.HealthChange < 0f &&
                (_componentCharacter.ComponentHealth.Health < 0.5f ||
                 _componentCharacter.ComponentVitalStats.Sleep > 0.5f))
            {
                WakeUp();
            }

            if (_componentCharacter.ComponentVitalStats.Wetness > _minWetness + 0.05f &&
                _componentCharacter.ComponentVitalStats.Sleep > 0.2f)
            {
                WakeUp();
                if (runGui)
                {
                    _subsystemTime.QueueGameTimeDelayedExecution(
                        _subsystemTime.GameTime + 1.0,
                        delegate
                        {
                            _componentCharacter.ComponentGui.DisplaySmallMessage(
                                LanguageManager.Get(_typeName, 6),
                                Color.White,
                                true,
                                true
                            );
                        }
                    );
                }
            }

            if (runGui && _sleepStartTime.HasValue)
            {
                var num = (float)(_subsystemGameInfo.TotalElapsedGameTime - _sleepStartTime.Value);
                if (_allowManualWakeUp && num > 10f)
                {
                    if (_componentCharacter.GameWidget.Input.Any &&
                        !DialogsManager.HasDialogs(_componentCharacter.GameWidget))
                    {
                        _componentCharacter.GameWidget.Input.Clear();
                        WakeUp();
                        _subsystemTime.QueueGameTimeDelayedExecution(
                            _subsystemTime.GameTime + 2.0,
                            delegate
                            {
                                _componentCharacter.ComponentGui.DisplaySmallMessage(
                                    LanguageManager.Get(_typeName, 7),
                                    Color.White,
                                    true,
                                    false
                                );
                            }
                        );
                    }

                    _messageFactor = MathUtils.Min(_messageFactor + 0.5f * Time.FrameDuration, 1f);
                    _componentCharacter.ComponentScreenOverlays.Message = LanguageManager.Get(_typeName, 8);
                    _componentCharacter.ComponentScreenOverlays.MessageFactor = _messageFactor;
                }

                if (!_allowManualWakeUp && num > 5f)
                {
                    _messageFactor = MathUtils.Min(_messageFactor + 1f * Time.FrameDuration, 1f);
                    _componentCharacter.ComponentScreenOverlays.Message = LanguageManager.Get(_typeName, 9);
                    _componentCharacter.ComponentScreenOverlays.MessageFactor = _messageFactor;
                }
            }
        }
        else
        {
            _sleepFactor = MathUtils.Max(_sleepFactor - 1f * Time.FrameDuration, 0f);
        }

        if (!runGui)
        {
            return;
        }

        _componentCharacter.ComponentScreenOverlays.BlackoutFactor =
            MathUtils.Max(_componentCharacter.ComponentScreenOverlays.BlackoutFactor, _sleepFactor);
        if (_sleepFactor <= 0.01f)
        {
            return;
        }

        _componentCharacter.ComponentScreenOverlays.FloatingMessage = LanguageManager.Get(_typeName, 10);
        _componentCharacter.ComponentScreenOverlays.FloatingMessageFactor =
            MathUtils.Saturate(10f * (_sleepFactor - 0.9f));
    }

    public bool CanSleep(out string reason)
    {
        var block = _componentCharacter.ComponentBody.StandingOnValue.HasValue
            ? CurrentModRuntime.Value!.RequireWorldCatalog()
                .Blocks[Terrain.ExtractContents(_componentCharacter.ComponentBody.StandingOnValue.Value)]
            : null;

        if (block is null || _componentCharacter.ComponentBody.ImmersionDepth > 0f)
        {
            reason = LanguageManager.Get(_typeName, 1);
            return false;
        }

        if (block.SleepSuitability == 0f)
        {
            reason = LanguageManager.Get(_typeName, 2);
            return false;
        }

        if (_componentCharacter.ComponentVitalStats.Sleep > 0.99f)
        {
            reason = LanguageManager.Get(_typeName, 3);
            return false;
        }

        if (_componentCharacter.ComponentVitalStats.Wetness > 0.95f)
        {
            reason = LanguageManager.Get(_typeName, 4);
            return false;
        }

        for (var i = -1; i <= 1; i++)
        {
            for (var j = -1; j <= 1; j++)
            {
                var start = _componentCharacter.ComponentBody.Position + new Vector3(i, 1f, j);
                var end = new Vector3(start.X, 255f, start.Z);
                if (_subsystemTerrain
                    .Raycast(start, end, false, true, (value, _) => Terrain.ExtractContents(value) != 0)
                    .HasValue)
                {
                    continue;
                }

                reason = LanguageManager.Get(_typeName, 5);
                return false;
            }
        }

        reason = string.Empty;
        return true;
    }

    public void Sleep(bool allowManualWakeup)
    {
        if (CommonLib.WorkType == WorkType.Client)
        {
            NetworkSender.SendToServer(
                new ComponentSleepPackage(
                    Entity.EntityId,
                    ComponentSleepPackage.EventType.SleepRequest,
                    allowManualWakeup
                )
            );
        }
        else
        {
            NetSleep(allowManualWakeup);
            NetworkSender.SendToObservers(
                Entity,
                new ComponentSleepPackage(
                    Entity.EntityId,
                    ComponentSleepPackage.EventType.Sleep,
                    allowManualWakeup,
                    true
                )
            );
        }
    }

    public void WakeUp()
    {
        if (CommonLib.WorkType == WorkType.Client)
        {
            NetworkSender.SendToServer(
                new ComponentSleepPackage(
                    Entity.EntityId,
                    ComponentSleepPackage.EventType.WakeupRequest
                )
            );
        }
        else
        {
            NetWakeUp();
            NetworkSender.SendToObservers(
                Entity,
                new ComponentSleepPackage(Entity.EntityId, ComponentSleepPackage.EventType.WakeUp)
            );
        }
    }

    public void NetSleep(bool allowManualWakeup)
    {
        if (IsSleeping)
        {
            return;
        }

        _sleepStartTime = _subsystemGameInfo.TotalElapsedGameTime;
        _allowManualWakeUp = allowManualWakeup;
        _minWetness = float.MaxValue;
        _messageFactor = 0f;
        _componentCharacter.CharacterStats.TimesWentToSleep++;
    }

    public void NetWakeUp()
    {
        if (!_sleepStartTime.HasValue)
        {
            return;
        }

        _sleepStartTime = null;
        _componentCharacter.CharacterData.SpawnPosition =
            _componentCharacter.ComponentBody.Position + new Vector3(0f, 0.1f, 0f);
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        _subsystemCharacters = Project.FindSubsystem<SubsystemCharacters>(true)!;
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        SubsystemUpdate = Project.FindSubsystem<SubsystemUpdate>(true)!;
        _subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true)!;
        _subsystemTimeOfDay = Project.FindSubsystem<SubsystemTimeOfDay>(true)!;
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _componentCharacter = Entity.FindComponent<ComponentCharacter>(true)!;
        _sleepStartTime = valuesDictionary.GetValue<double>("SleepStartTime");
        _allowManualWakeUp = valuesDictionary.GetValue<bool>("AllowManualWakeUp");
        if (_sleepStartTime == 0.0)
        {
            _sleepStartTime = null;
        }

        if (_sleepStartTime.HasValue)
        {
            _sleepFactor = 1f;
            _minWetness = float.MaxValue;
        }

        _componentCharacter.ComponentHealth.Attacked += delegate
        {
            if (IsSleeping && _componentCharacter.ComponentVitalStats.Sleep > 0.25f)
            {
                WakeUp();
            }
        };
    }

    public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
    {
        valuesDictionary.SetValue("SleepStartTime", _sleepStartTime ?? 0.0);
        valuesDictionary.SetValue("AllowManualWakeUp", _allowManualWakeUp);
    }
}
