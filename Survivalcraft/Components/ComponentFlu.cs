using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Network.Transport;

namespace Game.Components;

public class ComponentFlu : Component, IUpdateable
{
    private const string _typeName = nameof(ComponentFlu);

    private float _blackoutDuration;

    private float _blackoutFactor;

    private ComponentCharacter _componentCharacter = null!;

    public float CoughDuration;

    public float FluDuration;

    public float FluOnset;

    private double _lastCoughTime = -1000.0;

    private double _lastEffectTime = -1000.0;

    private double _lastMessageTime = -1000.0;

    private readonly Random _random = new();

    public float SneezeDuration;

    private SubsystemAudio _subsystemAudio = null!;

    private SubsystemGameInfo _subsystemGameInfo = null!;

    private SubsystemParticles _subsystemParticles = null!;

    private SubsystemTerrain _subsystemTerrain = null!;

    private SubsystemTime _subsystemTime = null!;

    public bool HasFlu => FluDuration > 0f;

    public bool IsCoughing => CoughDuration > 0f;

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        if (GameManager.Role != WorldRole.RemoteClient && Time.PeriodicEvent(1.0, 0.5))
        {
            if (_componentCharacter.CharacterData.Client is { } owner)
            {
                NetworkSender.SendTo(
                    owner,
                    EntityStatePackageBuilder.Capture(this, ComponentFluPackage.EventType.SyncStat)
                );
            }
        }

        if (_subsystemGameInfo.WorldSettings.GameMode == GameMode.Creative ||
            !_subsystemGameInfo.WorldSettings.AreAdventureSurvivalMechanicsEnabled)
        {
            FluDuration = 0f;
            FluOnset = 0f;
            return;
        }

        if (FluDuration > 0f)
        {
            FluOnset = 0f;
            var num = _componentCharacter.ComponentVitalStats.Temperature switch
            {
                > 16f => 2f,
                > 12f => 1.5f,
                < 8f => 0.5f,
                _ => 1f
            };
            FluDuration = MathUtils.Max(FluDuration - num * dt, 0f);
            if (_componentCharacter.ComponentHealth.Health > 0f &&
                !_componentCharacter.ComponentSleep.IsSleeping &&
                _subsystemTime.PeriodicGameTimeEvent(5.0, -0.0099999997764825821) &&
                _subsystemTime.GameTime - _lastEffectTime > 13.0)
            {
                if (GameManager.Role != WorldRole.RemoteClient)
                {
                    FluEffect();
                    NetworkSender.SendToObservers(
                        Entity,
                        EntityStatePackageBuilder.Capture(this, ComponentFluPackage.EventType.FluEffect)
                    );
                }
            }
        }
        else if (_componentCharacter.ComponentVitalStats.Temperature < 6f)
        {
            var num2 = 13f;
            FluOnset += dt;
            if (FluOnset > 120f)
            {
                num2 = 9f;
                if (_subsystemTime.PeriodicGameTimeEvent(1.0, 0.0) && _random.Bool(0.025f))
                {
                    if (GameManager.Role != WorldRole.RemoteClient)
                    {
                        StartFlu();
                        NetworkSender.SendToObservers(
                            Entity,
                            EntityStatePackageBuilder.Capture(this, ComponentFluPackage.EventType.StartFlu)
                        );
                    }
                }

                if (_subsystemTime.GameTime - _lastMessageTime > 60.0)
                {
                    _lastMessageTime = _subsystemTime.GameTime;
                    _componentCharacter.ComponentGui.DisplaySmallMessage(
                        LanguageManager.Get(_typeName, 1),
                        Color.White,
                        true,
                        true
                    );
                }
            }

            if (FluOnset > 60f &&
                _subsystemTime.PeriodicGameTimeEvent(num2, -0.0099999997764825821) &&
                _random.Bool(0.75f))
            {
                if (GameManager.Role != WorldRole.RemoteClient)
                {
                    Sneeze();
                    NetworkSender.SendToObservers(
                        Entity,
                        EntityStatePackageBuilder.Capture(this, ComponentFluPackage.EventType.Sneeze)
                    );
                }
            }
        }
        else
        {
            FluOnset = 0f;
        }

        if ((CoughDuration > 0f || SneezeDuration > 0f) &&
            _componentCharacter.ComponentHealth.Health > 0f &&
            !_componentCharacter.ComponentSleep.IsSleeping)
        {
            CoughDuration = MathUtils.Max(CoughDuration - dt, 0f);
            SneezeDuration = MathUtils.Max(SneezeDuration - dt, 0f);
            var num3 = MathUtils.DegToRad(
                MathUtils.Lerp(
                    -35f,
                    -65f,
                    SimplexNoise.Noise(4f * (float)MathUtils.Remainder(_subsystemTime.GameTime, 10000.0))
                )
            );
            _componentCharacter.ComponentLocomotion.LookOrder = new Vector2(
                _componentCharacter.ComponentLocomotion.LookOrder.X,
                MathUtils.Clamp(num3 - _componentCharacter.ComponentLocomotion.LookAngles.Y, -3f, 3f)
            );
            if (_random.Bool(2f * dt) && GameManager.Role != WorldRole.RemoteClient)
            {
                var vector = -1.2f * _componentCharacter.ComponentCreatureModel.EyeRotation.GetForwardVector();
                _componentCharacter.ComponentBody.ApplyImpulse(vector);
            }
        }

        if (_blackoutDuration > 0f)
        {
            _blackoutDuration = MathUtils.Max(_blackoutDuration - dt, 0f);
            _blackoutFactor = MathUtils.Min(_blackoutFactor + 0.5f * dt, 0.95f);
        }
        else if (_blackoutFactor > 0f)
        {
            _blackoutFactor = MathUtils.Max(_blackoutFactor - 0.5f * dt, 0f);
        }

        _componentCharacter.ComponentScreenOverlays.BlackoutFactor = MathUtils.Max(
            _blackoutFactor,
            _componentCharacter.ComponentScreenOverlays.BlackoutFactor
        );
    }

    public void StartFlu()
    {
        if (FluDuration == 0f)
        {
            _componentCharacter.CharacterStats.TimesHadFlu++;
        }

        FluDuration = 900f;
        _subsystemTime.QueueGameTimeDelayedExecution(
            _subsystemTime.GameTime + 10.0,
            delegate { _componentCharacter.ComponentVitalStats.MakeSleepy(0.2f); }
        );
    }


    public void Sneeze()
    {
        SneezeDuration = 1f;
        _componentCharacter.ComponentCreatureSounds.PlaySneezeSound();
        Project.FindSubsystem<SubsystemNoise>(true)!.MakeNoise(_componentCharacter.ComponentBody.Position, 0.25f, 10f);
    }


    public void Cough()
    {
        _lastCoughTime = _subsystemTime.GameTime;
        CoughDuration = 4f;
        _componentCharacter.ComponentCreatureSounds.PlayCoughSound();
        Project.FindSubsystem<SubsystemNoise>(true)!.MakeNoise(_componentCharacter.ComponentBody.Position, 0.25f, 10f);
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        _subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true)!;
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true)!;
        _subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true)!;
        _componentCharacter = Entity.FindComponent<ComponentCharacter>(true)!;
        FluDuration = valuesDictionary.GetValue<float>("FluDuration");
        FluOnset = valuesDictionary.GetValue<float>("FluOnset");
    }

    public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
    {
        valuesDictionary.SetValue("FluDuration", FluDuration);
        valuesDictionary.SetValue("FluOnset", FluOnset);
    }

    public void FluEffect()
    {
        _lastEffectTime = _subsystemTime.GameTime;
        _blackoutDuration = MathUtils.Lerp(4f, 2f, _componentCharacter.ComponentHealth.Health);
        var injury = MathUtils.Min(0.1f, _componentCharacter.ComponentHealth.Health - 0.175f);
        if (injury > 0f)
        {
            _subsystemTime.QueueGameTimeDelayedExecution(
                _subsystemTime.GameTime + 0.75,
                delegate
                {
                    _componentCharacter.ComponentHealth.Injure(injury, null, false, LanguageManager.Get(_typeName, 4));
                }
            );
        }

        if (Time.FrameStartTime - _lastMessageTime > 60.0)
        {
            _lastMessageTime = Time.FrameStartTime;
            _subsystemTime.QueueGameTimeDelayedExecution(
                _subsystemTime.GameTime + 1.5,
                delegate
                {
                    _componentCharacter.ComponentGui.DisplaySmallMessage(
                        _componentCharacter.ComponentVitalStats.Temperature < 8f
                            ? LanguageManager.Get(_typeName, 2)
                            : LanguageManager.Get(_typeName, 3),
                        Color.White,
                        true,
                        true
                    );
                }
            );
        }

        if (CoughDuration == 0f && (_subsystemTime.GameTime - _lastCoughTime > 40.0 || _random.Bool(0.5f)))
        {
            Cough();
        }
        else if (SneezeDuration == 0f)
        {
            Sneeze();
        }
    }
}
