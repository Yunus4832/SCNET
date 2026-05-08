using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;
using Game.NetWork;
using Game.NetWork.Packages;

namespace Game.Components;

public class ComponentSickness : Component, IUpdateable
{
    public const string Name = "ComponentSickness";

    private ComponentPlayer _componentPlayer = null!;

    private float _greenoutDuration;

    private float _greenoutFactor;

    private double? _lastMessageTime;

    private double? _lastNauseaTime;

    private double? _lastPukeTime;

    private PukeParticleSystem? _pukeParticleSystem;

    public float SicknessDuration;

    private SubsystemGameInfo _subsystemGameInfo = null!;

    private SubsystemParticles _subsystemParticles = null!;

    private SubsystemTerrain _subsystemTerrain = null!;

    private SubsystemTime _subsystemTime = null!;

    public bool IsSick => SicknessDuration > 0f;

    public bool IsPuking => _pukeParticleSystem != null;

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        if (_subsystemGameInfo.WorldSettings.GameMode == GameMode.Creative ||
            !_subsystemGameInfo.WorldSettings.AreAdventureSurvivalMechanicsEnabled)
        {
            SicknessDuration = 0f;
            return;
        }

        if (SicknessDuration > 0f)
        {
            //开服后，睡觉疾病恢复速度x5
            if (_componentPlayer.ComponentSleep is { IsSleeping: true, SubsystemUpdate.UpdatesPerFrame: 1 } &&
                CommonLib.WorkType == WorkType.Server)
            {
                dt *= _subsystemGameInfo.WorldSettings.RecoverFactor;
            }

            SicknessDuration = MathUtils.Max(SicknessDuration - dt, 0f);
            if (_componentPlayer.ComponentHealth.Health > 0f && !_componentPlayer.ComponentSleep.IsSleeping &&
                _subsystemTime.PeriodicGameTimeEvent(3.0, -0.0099999997764825821) && (!_lastNauseaTime.HasValue ||
                    _subsystemTime.GameTime - _lastNauseaTime > 15.0))
            {
                if (CommonLib.WorkType != WorkType.Client)
                {
                    NauseaEffect();
                    CommonLib.Net.QueuePackage(new ComponentSicknessPackage(this));
                }
            }
        }

        if (_pukeParticleSystem != null)
        {
            var num = MathUtils.DegToRad(MathUtils.Lerp(-35f, -60f,
                SimplexNoise.Noise(2f * (float)MathUtils.Remainder(_subsystemTime.GameTime, 10000.0))));
            _componentPlayer.ComponentLocomotion.LookOrder = new Vector2(
                _componentPlayer.ComponentLocomotion.LookOrder.X,
                MathUtils.Clamp(num - _componentPlayer.ComponentLocomotion.LookAngles.Y, -2f, 2f));
            var upVector = _componentPlayer.ComponentCreatureModel.EyeRotation.GetUpVector();
            var forwardVector = _componentPlayer.ComponentCreatureModel.EyeRotation.GetForwardVector();
            _pukeParticleSystem.Position = _componentPlayer.ComponentCreatureModel.EyePosition - 0.08f * upVector +
                                           0.3f * forwardVector;
            _pukeParticleSystem.Direction = Vector3.Normalize(forwardVector + 0.5f * upVector);
            if (_pukeParticleSystem.IsStopped)
            {
                _pukeParticleSystem = null;
            }
        }

        if (_greenoutDuration > 0f)
        {
            _greenoutDuration = MathUtils.Max(_greenoutDuration - dt, 0f);
            _greenoutFactor = MathUtils.Min(_greenoutFactor + 0.5f * dt, 0.95f);
        }
        else if (_greenoutFactor > 0f)
        {
            _greenoutFactor = MathUtils.Max(_greenoutFactor - 0.5f * dt, 0f);
        }

        _componentPlayer.ComponentScreenOverlays.GreenOutFactor = MathUtils.Max(_greenoutFactor,
            _componentPlayer.ComponentScreenOverlays.GreenOutFactor);
    }

    public void StartSickness()
    {
        if (CommonLib.WorkType == WorkType.Client)
        {
            return;
        }

        StartSicknessNet();
    }

    public void StartSicknessNet()
    {
        if (SicknessDuration == 0f)
        {
            _componentPlayer.PlayerStats.TimesWasSick++;
        }

        SicknessDuration = 900f;
    }

    public void EndSickness()
    {
        if (CommonLib.WorkType == WorkType.Client)
        {
            return;
        }

        CommonLib.Net.QueuePackage(new ComponentSicknessPackage(this));
        SicknessDuration = 0f;
    }

    public void Puke()
    {
        _lastPukeTime = _subsystemTime.GameTime;
        _pukeParticleSystem = new PukeParticleSystem(_subsystemTerrain);
        _subsystemParticles.AddParticleSystem(_pukeParticleSystem);
        _componentPlayer.ComponentCreatureSounds.PlayPukeSound();
        Project.FindSubsystem<SubsystemNoise>(true)!.MakeNoise(_componentPlayer.ComponentBody.Position, 0.25f, 10f);
        _greenoutDuration = 0.8f;
        _componentPlayer.PlayerStats.TimesPuked++;
    }


    public void NauseaEffect()
    {
        _lastNauseaTime = _subsystemTime.GameTime;
        _componentPlayer.ComponentCreatureSounds.PlayMoanSound();
        var injury = MathUtils.Min(0.1f, _componentPlayer.ComponentHealth.Health - 0.075f);
        if (injury > 0f)
        {
            _subsystemTime.QueueGameTimeDelayedExecution(_subsystemTime.GameTime + 0.75,
                delegate
                {
                    _componentPlayer.ComponentHealth.Injure(injury, null, false, LanguageControl.Get(Name, 1));
                });
        }

        if (_pukeParticleSystem == null &&
            (!_lastPukeTime.HasValue || _subsystemTime.GameTime - _lastPukeTime > 50.0))
        {
            Puke();
        }
        else
        {
            _greenoutDuration = MathUtils.Lerp(4f, 2f, _componentPlayer.ComponentHealth.Health);
            if (!_lastMessageTime.HasValue || Time.FrameStartTime - _lastMessageTime > 60.0)
            {
                _lastMessageTime = Time.FrameStartTime;
                _subsystemTime.QueueGameTimeDelayedExecution(_subsystemTime.GameTime + 1.5,
                    delegate
                    {
                        _componentPlayer.ComponentGui.DisplaySmallMessage(LanguageControl.Get(Name, 2), Color.White,
                            true, true);
                    });
            }
        }
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        _subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true)!;
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true)!;
        _componentPlayer = Entity.FindComponent<ComponentPlayer>(true)!;
        SicknessDuration = valuesDictionary.GetValue<float>("SicknessDuration");
    }

    public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
    {
        valuesDictionary.SetValue("SicknessDuration", SicknessDuration);
    }
}
