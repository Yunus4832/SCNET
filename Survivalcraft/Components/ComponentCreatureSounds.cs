using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;
using Game.NetWork;
using Game.NetWork.Packages;

namespace Game.Components;

public class ComponentCreatureSounds : Component
{
    private string _attackSound = null!;

    private float _attackSoundMinDistance;

    private ComponentCreature _componentCreature = null!;

    private string _coughSound = null!;

    private float _coughSoundMinDistance;

    private string _idleSound = null!;

    private float _idleSoundMinDistance;

    private double _lastCoughingSoundTime = -1000.0;

    private double _lastPukeSoundTime = -1000.0;

    private double _lastSoundTime = -1000.0;

    private string _moanSound = null!;

    private float _moanSoundMinDistance;

    private string _painSound = null!;

    private float _painSoundMinDistance;

    private string _pukeSound = null!;

    private float _pukeSoundMinDistance;

    private readonly Random _random = new();

    private string _sneezeSound = null!;

    private float _sneezeSoundMinDistance;

    private SubsystemAudio _subsystemAudio = null!;

    private SubsystemSoundMaterials _subsystemSoundMaterials = null!;

    private SubsystemTime _subsystemTime = null!;

    public void PlayIdleSound(bool skipIfRecentlyPlayed)
    {
        if (CommonLib.WorkType == WorkType.Client)
        {
            return;
        }

        PlayIdleSoundLogic(skipIfRecentlyPlayed);
    }

    public void PlayIdleSoundLogic(bool skipIfRecentlyPlayed)
    {
        if (string.IsNullOrEmpty(_idleSound) ||
            !(_subsystemTime.GameTime > _lastSoundTime + (skipIfRecentlyPlayed ? 12f : 1f)))
        {
            return;
        }

        _lastSoundTime = _subsystemTime.GameTime;
        _subsystemAudio.PlayRandomSound(_idleSound, 1f, _random.Float(-0.1f, 0.1f),
            _componentCreature.ComponentBody.Position, _idleSoundMinDistance, false);
        CommonLib.Net.QueuePackage(new ComponentBehaviorPackage(this, 0, skipIfRecentlyPlayed));
    }

    public void PlayPainSound()
    {
        if (CommonLib.WorkType == WorkType.Client)
        {
            return;
        }

        PlayPainSoundLogic();
    }

    public void PlayPainSoundLogic()
    {
        if (string.IsNullOrEmpty(_painSound) || !(_subsystemTime.GameTime > _lastSoundTime + 1.0))
        {
            return;
        }

        _lastSoundTime = _subsystemTime.GameTime;
        _subsystemAudio.PlayRandomSound(_painSound, 1f, _random.Float(-0.1f, 0.1f),
            _componentCreature.ComponentBody.Position, _painSoundMinDistance, false);
        CommonLib.Net.QueuePackage(new ComponentBehaviorPackage(this, 1));
    }

    public void PlayMoanSound()
    {
        if (CommonLib.WorkType == WorkType.Client)
        {
            return;
        }

        PlayMoanSoundLogic();
    }

    public void PlayMoanSoundLogic()
    {
        if (string.IsNullOrEmpty(_moanSound) || !(_subsystemTime.GameTime > _lastSoundTime + 1.0))
        {
            return;
        }

        _lastSoundTime = _subsystemTime.GameTime;
        _subsystemAudio.PlayRandomSound(_moanSound, 1f, _random.Float(-0.1f, 0.1f),
            _componentCreature.ComponentBody.Position, _moanSoundMinDistance, false);
        CommonLib.Net.QueuePackage(new ComponentBehaviorPackage(this, 2));
    }

    public void PlaySneezeSound()
    {
        if (CommonLib.WorkType == WorkType.Client)
        {
            return;
        }

        PlaySneezeSoundLogic();
    }

    public void PlaySneezeSoundLogic()
    {
        if (string.IsNullOrEmpty(_sneezeSound) || !(_subsystemTime.GameTime > _lastSoundTime + 1.0))
        {
            return;
        }

        _lastSoundTime = _subsystemTime.GameTime;
        _subsystemAudio.PlayRandomSound(_sneezeSound, 1f, _random.Float(-0.1f, 0.1f),
            _componentCreature.ComponentBody.Position, _sneezeSoundMinDistance, false);
        CommonLib.Net.QueuePackage(new ComponentBehaviorPackage(this, 3));
    }

    public void PlayCoughSound()
    {
        if (CommonLib.WorkType == WorkType.Client)
        {
            return;
        }

        PlayCoughSoundLogic();
    }

    public void PlayCoughSoundLogic()
    {
        if (string.IsNullOrEmpty(_coughSound) || !(_subsystemTime.GameTime > _lastCoughingSoundTime + 1.0))
        {
            return;
        }

        _lastCoughingSoundTime = _subsystemTime.GameTime;
        _subsystemAudio.PlayRandomSound(_coughSound, 1f, _random.Float(-0.1f, 0.1f),
            _componentCreature.ComponentBody.Position, _coughSoundMinDistance, false);
        CommonLib.Net.QueuePackage(new ComponentBehaviorPackage(this, 4));
    }

    public void PlayPukeSound()
    {
        if (CommonLib.WorkType == WorkType.Client)
        {
            return;
        }

        PlayPukeSoundLogic();
    }

    public void PlayPukeSoundLogic()
    {
        if (string.IsNullOrEmpty(_pukeSound) || !(_subsystemTime.GameTime > _lastPukeSoundTime + 1.0))
        {
            return;
        }

        _lastPukeSoundTime = _subsystemTime.GameTime;
        _subsystemAudio.PlayRandomSound(_pukeSound, 1f, _random.Float(-0.1f, 0.1f),
            _componentCreature.ComponentBody.Position, _pukeSoundMinDistance, false);
        CommonLib.Net.QueuePackage(new ComponentBehaviorPackage(this, 5));
    }

    public void PlayAttackSound()
    {
        if (CommonLib.WorkType == WorkType.Client)
        {
            return;
        }

        PlayAttackSoundLogic();
    }

    public void PlayAttackSoundLogic()
    {
        if (string.IsNullOrEmpty(_attackSound) || !(_subsystemTime.GameTime > _lastSoundTime + 1.0))
        {
            return;
        }

        _lastSoundTime = _subsystemTime.GameTime;
        _subsystemAudio.PlayRandomSound(_attackSound, 1f, _random.Float(-0.1f, 0.1f),
            _componentCreature.ComponentBody.Position, _attackSoundMinDistance, false);
        CommonLib.Net.QueuePackage(new ComponentBehaviorPackage(this, 6));
    }

    public bool PlayFootstepSound(float loudnessMultiplier)
    {
        return _subsystemSoundMaterials.PlayFootstepSound(_componentCreature, loudnessMultiplier);
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true)!;
        _subsystemSoundMaterials = Project.FindSubsystem<SubsystemSoundMaterials>(true)!;
        _componentCreature = Entity.FindComponent<ComponentCreature>(true)!;
        _idleSound = valuesDictionary.GetValue<string>("IdleSound");
        _painSound = valuesDictionary.GetValue<string>("PainSound");
        _moanSound = valuesDictionary.GetValue<string>("MoanSound");
        _sneezeSound = valuesDictionary.GetValue<string>("SneezeSound");
        _coughSound = valuesDictionary.GetValue<string>("CoughSound");
        _pukeSound = valuesDictionary.GetValue<string>("PukeSound");
        _attackSound = valuesDictionary.GetValue<string>("AttackSound");
        _idleSoundMinDistance = valuesDictionary.GetValue<float>("IdleSoundMinDistance");
        _painSoundMinDistance = valuesDictionary.GetValue<float>("PainSoundMinDistance");
        _moanSoundMinDistance = valuesDictionary.GetValue<float>("MoanSoundMinDistance");
        _sneezeSoundMinDistance = valuesDictionary.GetValue<float>("SneezeSoundMinDistance");
        _coughSoundMinDistance = valuesDictionary.GetValue<float>("CoughSoundMinDistance");
        _pukeSoundMinDistance = valuesDictionary.GetValue<float>("PukeSoundMinDistance");
        _attackSoundMinDistance = valuesDictionary.GetValue<float>("AttackSoundMinDistance");
    }
}
