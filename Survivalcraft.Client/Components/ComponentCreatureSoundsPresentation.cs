using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Components.Behavior;

namespace Game.Components;

public class ComponentCreatureSoundsPresentation : Component
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

    private bool _inactive;

    private string _sneezeSound = null!;

    private float _sneezeSoundMinDistance;

    private SubsystemAudio _subsystemAudio = null!;

    private SubsystemSoundMaterials _subsystemSoundMaterials = null!;

    private SubsystemTime _subsystemTime = null!;

    public bool PlayIdleSound(bool skipIfRecentlyPlayed)
    {
        if (_inactive ||
            !WorldRuntimeContext.IsProjectActive(Project) ||
            !WorldRuntimeContext.For(Project).HasPresentation ||
            !TryBeginSound(_idleSound, ref _lastSoundTime, skipIfRecentlyPlayed ? 12f : 1f))
        {
            return false;
        }

        _subsystemAudio.PlayRandomSound(
            _idleSound,
            1f,
            _random.Float(-0.1f, 0.1f),
            _componentCreature.ComponentBody.Position,
            _idleSoundMinDistance,
            false
        );
        return true;
    }

    public bool PlayPainSound()
    {
        if (_inactive ||
            !WorldRuntimeContext.IsProjectActive(Project) ||
            !WorldRuntimeContext.For(Project).HasPresentation ||
            !TryBeginSound(_painSound, ref _lastSoundTime, 1.0))
        {
            return false;
        }

        _subsystemAudio.PlayRandomSound(
            _painSound,
            1f,
            _random.Float(-0.1f, 0.1f),
            _componentCreature.ComponentBody.Position,
            _painSoundMinDistance,
            false
        );
        return true;
    }

    public bool PlayMoanSound()
    {
        if (_inactive ||
            !WorldRuntimeContext.IsProjectActive(Project) ||
            !WorldRuntimeContext.For(Project).HasPresentation ||
            !TryBeginSound(_moanSound, ref _lastSoundTime, 1.0))
        {
            return false;
        }

        _subsystemAudio.PlayRandomSound(
            _moanSound,
            1f,
            _random.Float(-0.1f, 0.1f),
            _componentCreature.ComponentBody.Position,
            _moanSoundMinDistance,
            false
        );
        return true;
    }

    public bool PlaySneezeSound()
    {
        if (_inactive ||
            !WorldRuntimeContext.IsProjectActive(Project) ||
            !WorldRuntimeContext.For(Project).HasPresentation ||
            !TryBeginSound(_sneezeSound, ref _lastSoundTime, 1.0))
        {
            return false;
        }

        _subsystemAudio.PlayRandomSound(
            _sneezeSound,
            1f,
            _random.Float(-0.1f, 0.1f),
            _componentCreature.ComponentBody.Position,
            _sneezeSoundMinDistance,
            false
        );
        return true;
    }

    public bool PlayCoughSound()
    {
        if (_inactive ||
            !WorldRuntimeContext.IsProjectActive(Project) ||
            !WorldRuntimeContext.For(Project).HasPresentation ||
            !TryBeginSound(_coughSound, ref _lastCoughingSoundTime, 1.0))
        {
            return false;
        }

        _subsystemAudio.PlayRandomSound(
            _coughSound,
            1f,
            _random.Float(-0.1f, 0.1f),
            _componentCreature.ComponentBody.Position,
            _coughSoundMinDistance,
            false
        );
        return true;
    }

    public bool PlayPukeSound()
    {
        if (_inactive ||
            !WorldRuntimeContext.IsProjectActive(Project) ||
            !WorldRuntimeContext.For(Project).HasPresentation ||
            !TryBeginSound(_pukeSound, ref _lastPukeSoundTime, 1.0))
        {
            return false;
        }

        _subsystemAudio.PlayRandomSound(
            _pukeSound,
            1f,
            _random.Float(-0.1f, 0.1f),
            _componentCreature.ComponentBody.Position,
            _pukeSoundMinDistance,
            false
        );
        return true;
    }

    public bool PlayAttackSound()
    {
        if (_inactive ||
            !WorldRuntimeContext.IsProjectActive(Project) ||
            !WorldRuntimeContext.For(Project).HasPresentation ||
            !TryBeginSound(_attackSound, ref _lastSoundTime, 1.0))
        {
            return false;
        }

        _subsystemAudio.PlayRandomSound(
            _attackSound,
            1f,
            _random.Float(-0.1f, 0.1f),
            _componentCreature.ComponentBody.Position,
            _attackSoundMinDistance,
            false
        );
        return true;
    }

    public void PlayHowlSound()
    {
        if (!CanPlayBehaviorSound) { return; }

        var sound = Entity.FindComponent<ComponentHowlBehavior>()?.ValuesDictionary
            .GetValue("HowlSoundName", string.Empty);
        if (string.IsNullOrEmpty(sound)) { return; }

        _subsystemAudio.PlayRandomSound(
            sound,
            1f,
            _random.Float(-.1f, .1f),
            _componentCreature.ComponentBody.Position,
            10f,
            true
        );
    }

    public void PlayWingsSound()
    {
        if (!CanPlayBehaviorSound) { return; }

        _subsystemAudio.PlayRandomSound(
            "Audio/Creatures/Wings",
            .8f,
            _random.Float(-.1f, .2f),
            _componentCreature.ComponentBody.Position,
            3f,
            true
        );
    }

    public void PlayEggLaidSound()
    {
        if (!CanPlayBehaviorSound) { return; }

        var bounds = _componentCreature.ComponentBody.BoundingBox;
        _subsystemAudio.PlaySound(
            "Audio/EggLaid",
            1f,
            _random.Float(-.1f, .1f),
            (bounds.Min + bounds.Max) * .5f,
            2f,
            true
        );
    }

    private bool CanPlayBehaviorSound => !_inactive &&
                                         WorldRuntimeContext.IsProjectActive(Project) &&
                                         WorldRuntimeContext.For(Project).HasPresentation;

    private bool TryBeginSound(string sound, ref double lastTime, double interval)
    {
        if (string.IsNullOrEmpty(sound) || !(_subsystemTime.GameTime > lastTime + interval))
        {
            return false;
        }

        lastTime = _subsystemTime.GameTime;
        return true;
    }

    public bool PlayFootstepSound(float loudnessMultiplier)
    {
        if (_inactive ||
            !WorldRuntimeContext.IsProjectActive(Project) ||
            !WorldRuntimeContext.For(Project).HasPresentation) { return false; }

        return _subsystemSoundMaterials.PlayFootstepSound(_componentCreature, loudnessMultiplier);
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        if (_inactive ||
            !WorldRuntimeContext.IsProjectActive(Project) ||
            !WorldRuntimeContext.For(Project).HasPresentation) { return; }

        valuesDictionary = Entity.FindComponent<ComponentCreatureSounds>(true)!.ValuesDictionary;
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

    public override void OnEntityAdded() => _inactive = false;

    public override void OnEntityRemoved() => _inactive = true;

    public override void Dispose() => _inactive = true;
}
