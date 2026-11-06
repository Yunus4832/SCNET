using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public class ComponentCreatureSounds : Component
{
    private string _attackSound = null!;

    private string _coughSound = null!;

    private string _idleSound = null!;

    private double _lastCoughingSoundTime = -1000.0;

    private double _lastPukeSoundTime = -1000.0;

    private double _lastSoundTime = -1000.0;

    private double _lastFeedingSoundTime = -1000.0;

    private string _moanSound = null!;

    private string _painSound = null!;

    private string _pukeSound = null!;

    private string _sneezeSound = null!;

    private SubsystemTime _subsystemTime = null!;

    public void PlayIdleSound(bool skipIfRecentlyPlayed)
    {
        if (!WorldRuntimeContext.IsProjectActive(Project) || !WorldRuntimeContext.For(Project).IsAuthority)
        {
            return;
        }

        if (TryBeginSound(_idleSound, ref _lastSoundTime, skipIfRecentlyPlayed ? 12f : 1f))
        {
            NetworkSender.SendToObservers(Entity, BehaviorPackageBuilder.Capture(this, 0, skipIfRecentlyPlayed));
        }
    }

    public void PlayPainSound()
    {
        if (!WorldRuntimeContext.IsProjectActive(Project) || !WorldRuntimeContext.For(Project).IsAuthority)
        {
            return;
        }

        if (TryBeginSound(_painSound, ref _lastSoundTime, 1.0))
        {
            NetworkSender.SendToObservers(Entity, BehaviorPackageBuilder.Capture(this, 1));
        }
    }

    public void PlayMoanSound()
    {
        if (!WorldRuntimeContext.IsProjectActive(Project) || !WorldRuntimeContext.For(Project).IsAuthority)
        {
            return;
        }

        if (TryBeginSound(_moanSound, ref _lastSoundTime, 1.0))
        {
            NetworkSender.SendToObservers(Entity, BehaviorPackageBuilder.Capture(this, 2));
        }
    }

    public void PlaySneezeSound()
    {
        if (!WorldRuntimeContext.IsProjectActive(Project) || !WorldRuntimeContext.For(Project).IsAuthority)
        {
            return;
        }

        if (TryBeginSound(_sneezeSound, ref _lastSoundTime, 1.0))
        {
            NetworkSender.SendToObservers(Entity, BehaviorPackageBuilder.Capture(this, 3));
        }
    }

    public void PlayCoughSound()
    {
        if (!WorldRuntimeContext.IsProjectActive(Project) || !WorldRuntimeContext.For(Project).IsAuthority)
        {
            return;
        }

        if (TryBeginSound(_coughSound, ref _lastCoughingSoundTime, 1.0))
        {
            NetworkSender.SendToObservers(Entity, BehaviorPackageBuilder.Capture(this, 4));
        }
    }

    public void PlayPukeSound()
    {
        if (!WorldRuntimeContext.IsProjectActive(Project) || !WorldRuntimeContext.For(Project).IsAuthority)
        {
            return;
        }

        if (TryBeginSound(_pukeSound, ref _lastPukeSoundTime, 1.0))
        {
            NetworkSender.SendToObservers(Entity, BehaviorPackageBuilder.Capture(this, 5));
        }
    }

    public void PlayAttackSound()
    {
        if (!WorldRuntimeContext.IsProjectActive(Project) || !WorldRuntimeContext.For(Project).IsAuthority)
        {
            return;
        }

        if (TryBeginSound(_attackSound, ref _lastSoundTime, 1.0))
        {
            NetworkSender.SendToObservers(Entity, BehaviorPackageBuilder.Capture(this, 6));
        }
    }

    public void PlayFeedingSound()
    {
        if (!WorldRuntimeContext.IsProjectActive(Project) ||
            !WorldRuntimeContext.For(Project).IsAuthority ||
            _subsystemTime.GameTime <= _lastFeedingSoundTime + .15) { return; }

        _lastFeedingSoundTime = _subsystemTime.GameTime;
        NetworkSender.SendToObservers(Entity, BehaviorPackageBuilder.Capture(this, 7));
    }

    public void PlayHowlSound() => PublishBehaviorSound(8);

    public void PlayWingsSound() => PublishBehaviorSound(9);

    public void PlayEggLaidSound() => PublishBehaviorSound(10);

    private void PublishBehaviorSound(byte kind)
    {
        if (!WorldRuntimeContext.IsProjectActive(Project) || !WorldRuntimeContext.For(Project).IsAuthority) { return; }

        NetworkSender.SendToObservers(Entity, BehaviorPackageBuilder.Capture(this, kind));
    }

    private bool TryBeginSound(string sound, ref double lastTime, double interval)
    {
        if (string.IsNullOrEmpty(sound) || !(_subsystemTime.GameTime > lastTime + interval))
        {
            return false;
        }

        lastTime = _subsystemTime.GameTime;
        return true;
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _idleSound = valuesDictionary.GetValue<string>("IdleSound");
        _painSound = valuesDictionary.GetValue<string>("PainSound");
        _moanSound = valuesDictionary.GetValue<string>("MoanSound");
        _sneezeSound = valuesDictionary.GetValue<string>("SneezeSound");
        _coughSound = valuesDictionary.GetValue<string>("CoughSound");
        _pukeSound = valuesDictionary.GetValue<string>("PukeSound");
        _attackSound = valuesDictionary.GetValue<string>("AttackSound");
    }
}
