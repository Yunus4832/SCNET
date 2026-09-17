using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Network;
using Game.Network.Enums;
using Game.Network.Packages;

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

        if (PlayIdleSoundLocal(skipIfRecentlyPlayed))
        {
            CommonLib.Net.QueuePackage(new ComponentBehaviorPackage(this, 0, skipIfRecentlyPlayed));
        }
    }

    internal bool PlayIdleSoundLocal(bool skipIfRecentlyPlayed)
    {
        if (string.IsNullOrEmpty(_idleSound) ||
            !(_subsystemTime.GameTime > _lastSoundTime + (skipIfRecentlyPlayed ? 12f : 1f)))
        {
            return false;
        }

        _lastSoundTime = _subsystemTime.GameTime;
        _subsystemAudio.PlayRandomSound(_idleSound, 1f, _random.Float(-0.1f, 0.1f),
            _componentCreature.ComponentBody.Position, _idleSoundMinDistance, false);
        return true;
    }

    public void PlayPainSound()
    {
        if (CommonLib.WorkType == WorkType.Client)
        {
            return;
        }

        if (PlayPainSoundLocal())
        {
            CommonLib.Net.QueuePackage(new ComponentBehaviorPackage(this, 1));
        }
    }

    internal bool PlayPainSoundLocal()
    {
        if (string.IsNullOrEmpty(_painSound) || !(_subsystemTime.GameTime > _lastSoundTime + 1.0))
        {
            return false;
        }

        _lastSoundTime = _subsystemTime.GameTime;
        _subsystemAudio.PlayRandomSound(_painSound, 1f, _random.Float(-0.1f, 0.1f),
            _componentCreature.ComponentBody.Position, _painSoundMinDistance, false);
        return true;
    }

    public void PlayMoanSound()
    {
        if (CommonLib.WorkType == WorkType.Client)
        {
            return;
        }

        if (PlayMoanSoundLocal())
        {
            CommonLib.Net.QueuePackage(new ComponentBehaviorPackage(this, 2));
        }
    }

    internal bool PlayMoanSoundLocal()
    {
        if (string.IsNullOrEmpty(_moanSound) || !(_subsystemTime.GameTime > _lastSoundTime + 1.0))
        {
            return false;
        }

        _lastSoundTime = _subsystemTime.GameTime;
        _subsystemAudio.PlayRandomSound(_moanSound, 1f, _random.Float(-0.1f, 0.1f),
            _componentCreature.ComponentBody.Position, _moanSoundMinDistance, false);
        return true;
    }

    public void PlaySneezeSound()
    {
        if (CommonLib.WorkType == WorkType.Client)
        {
            return;
        }

        if (PlaySneezeSoundLocal())
        {
            CommonLib.Net.QueuePackage(new ComponentBehaviorPackage(this, 3));
        }
    }

    internal bool PlaySneezeSoundLocal()
    {
        if (string.IsNullOrEmpty(_sneezeSound) || !(_subsystemTime.GameTime > _lastSoundTime + 1.0))
        {
            return false;
        }

        _lastSoundTime = _subsystemTime.GameTime;
        _subsystemAudio.PlayRandomSound(_sneezeSound, 1f, _random.Float(-0.1f, 0.1f),
            _componentCreature.ComponentBody.Position, _sneezeSoundMinDistance, false);
        return true;
    }

    public void PlayCoughSound()
    {
        if (CommonLib.WorkType == WorkType.Client)
        {
            return;
        }

        if (PlayCoughSoundLocal())
        {
            CommonLib.Net.QueuePackage(new ComponentBehaviorPackage(this, 4));
        }
    }

    internal bool PlayCoughSoundLocal()
    {
        if (string.IsNullOrEmpty(_coughSound) || !(_subsystemTime.GameTime > _lastCoughingSoundTime + 1.0))
        {
            return false;
        }

        _lastCoughingSoundTime = _subsystemTime.GameTime;
        _subsystemAudio.PlayRandomSound(_coughSound, 1f, _random.Float(-0.1f, 0.1f),
            _componentCreature.ComponentBody.Position, _coughSoundMinDistance, false);
        return true;
    }

    public void PlayPukeSound()
    {
        if (CommonLib.WorkType == WorkType.Client)
        {
            return;
        }

        if (PlayPukeSoundLocal())
        {
            CommonLib.Net.QueuePackage(new ComponentBehaviorPackage(this, 5));
        }
    }

    internal bool PlayPukeSoundLocal()
    {
        if (string.IsNullOrEmpty(_pukeSound) || !(_subsystemTime.GameTime > _lastPukeSoundTime + 1.0))
        {
            return false;
        }

        _lastPukeSoundTime = _subsystemTime.GameTime;
        _subsystemAudio.PlayRandomSound(_pukeSound, 1f, _random.Float(-0.1f, 0.1f),
            _componentCreature.ComponentBody.Position, _pukeSoundMinDistance, false);
        return true;
    }

    public void PlayAttackSound()
    {
        if (CommonLib.WorkType == WorkType.Client)
        {
            return;
        }

        if (PlayAttackSoundLocal())
        {
            CommonLib.Net.QueuePackage(new ComponentBehaviorPackage(this, 6));
        }
    }

    internal bool PlayAttackSoundLocal()
    {
        if (string.IsNullOrEmpty(_attackSound) || !(_subsystemTime.GameTime > _lastSoundTime + 1.0))
        {
            return false;
        }

        _lastSoundTime = _subsystemTime.GameTime;
        _subsystemAudio.PlayRandomSound(_attackSound, 1f, _random.Float(-0.1f, 0.1f),
            _componentCreature.ComponentBody.Position, _attackSoundMinDistance, false);
        return true;
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
