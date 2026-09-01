using Engine.Audio;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Network;
using Game.Network.Enums;

namespace Game.Subsystems;

public class SubsystemAudio : Subsystem, IUpdateable
{
    private readonly Dictionary<string, Congestion> _congestions = new();

    private readonly List<Vector3> _listenerPositions = [];

    private readonly Dictionary<Sound, bool> _mutedSounds = new();

    private double _nextSoundTime;

    private readonly List<SoundInfo> _queuedSounds = [];

    private readonly Random _random = new();

    private readonly List<Sound> _sounds = [];

    private SubsystemTime _subsystemTime = null!;

    private SubsystemGameWidgets _subsystemViews = null!;

    public ReadOnlyList<Vector3> ListenerPositions => new(_listenerPositions);

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        _listenerPositions.Clear();
        if (CommonLib.WorkType == WorkType.Client)
        {
            foreach (var gameWidget in _subsystemViews.GameWidgets)
            {
                if (!gameWidget.PlayerData.IsMainPlayer)
                {
                    continue;
                }

                _listenerPositions.Add(gameWidget.ActiveCamera.ViewPosition);
            }
        }
        else
        {
            foreach (var gameWidget in _subsystemViews.GameWidgets)
            {
                _listenerPositions.Add(gameWidget.ActiveCamera.ViewPosition);
            }
        }

        if (!(_subsystemTime.GameTime >= _nextSoundTime))
        {
            return;
        }

        _nextSoundTime = double.MaxValue;
        var num = 0;
        while (num < _queuedSounds.Count)
        {
            var soundInfo = _queuedSounds[num];
            if (_subsystemTime.GameTime >= soundInfo.Time)
            {
                if (_subsystemTime.GameTimeFactor.CloseTo(1f) && !_subsystemTime.FixedTimeStep.HasValue &&
                    soundInfo.Volume * SettingsManager.Current.SoundsVolume > AudioManager.MinAudibleVolume &&
                    UpdateCongestion(soundInfo.Name, soundInfo.Volume))
                {
                    AudioManager.PlaySound(soundInfo.Name, soundInfo.Volume, soundInfo.Pitch, soundInfo.Pan);
                }

                _queuedSounds.RemoveAt(num);
            }
            else
            {
                _nextSoundTime = MathUtils.Min(_nextSoundTime, soundInfo.Time);
                num++;
            }
        }
    }

    public float CalculateListenerDistanceSquared(Vector3 p)
    {
        var num = float.MaxValue;
        foreach (var position in _listenerPositions)
        {
            var num2 = Vector3.DistanceSquared(position, p);
            if (num2 < num)
            {
                num = num2;
            }
        }

        return num;
    }

    public float CalculateListenerDistance(Vector3 p)
    {
        return MathUtils.Sqrt(CalculateListenerDistanceSquared(p));
    }

    public void Mute()
    {
        foreach (var sound in _sounds)
        {
            if (sound.State == SoundState.Playing)
            {
                _mutedSounds[sound] = true;
                sound.Pause();
            }
        }
    }

    public void Unmute()
    {
        foreach (var key in _mutedSounds.Keys)
        {
            key.Play();
        }

        _mutedSounds.Clear();
    }

    public void PlaySound(string name, float volume, float pitch, float pan, float delay)
    {
        var num = _subsystemTime.GameTime + delay;
        _nextSoundTime = MathUtils.Min(_nextSoundTime, num);
        _queuedSounds.Add(new SoundInfo
        {
            Time = num,
            Name = name,
            Volume = volume,
            Pitch = pitch,
            Pan = pan
        });
    }

    public void PlaySound(string name, float volume, float pitch, Vector3 position, float minDistance, float delay)
    {
        var num = CalculateVolume(CalculateListenerDistance(position), minDistance);
        PlaySound(name, volume * num, pitch, 0f, delay);
    }

    public void PlaySound(string name, float volume, float pitch, Vector3 position, float minDistance, bool autoDelay)
    {
        var num = CalculateVolume(CalculateListenerDistance(position), minDistance);
        PlaySound(name, volume * num, pitch, 0f, autoDelay ? CalculateDelay(position) : 0f);
    }

    public void PlayRandomSound(string directory, float volume, float pitch, float pan, float delay)
    {
        var readOnlyList = ContentManager.List(directory);
        if (readOnlyList.Count > 0)
        {
            var index = _random.Int(0, readOnlyList.Count - 1);
            PlaySound(readOnlyList[index].ContentPath, volume, pitch, pan, delay);
        }
        else
        {
            Log.Warning("Sounds directory \"{0}\" not found or empty.", directory);
        }
    }

    public void PlayRandomSound(string directory, float volume, float pitch, Vector3 position, float minDistance,
        float delay)
    {
        var num = CalculateVolume(CalculateListenerDistance(position), minDistance);
        PlayRandomSound(directory, volume * num, pitch, 0f, delay);
    }

    public void PlayRandomSound(string directory, float volume, float pitch, Vector3 position, float minDistance,
        bool autoDelay)
    {
        var num = CalculateVolume(CalculateListenerDistance(position), minDistance);
        PlayRandomSound(directory, volume * num, pitch, 0f, autoDelay ? CalculateDelay(position) : 0f);
    }

    public Sound CreateSound(string name)
    {
        var sound = new Sound(ContentManager.Get<SoundBuffer>(name));
        _sounds.Add(sound);
        return sound;
    }

    public float CalculateVolume(float distance, float minDistance, float rolloffFactor = 2f)
    {
        if (distance > minDistance)
        {
            return minDistance / (minDistance + MathUtils.Max(rolloffFactor * (distance - minDistance), 0f));
        }

        return 1f;
    }

    public float CalculateDelay(Vector3 position)
    {
        return CalculateDelay(CalculateListenerDistance(position));
    }

    public float CalculateDelay(float distance)
    {
        return MathUtils.Min(distance / 100f, 5f);
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _subsystemViews = Project.FindSubsystem<SubsystemGameWidgets>(true)!;
    }

    public override void Dispose()
    {
        foreach (var sound in _sounds)
        {
            sound.Dispose();
        }
    }

    public bool UpdateCongestion(string name, float volume)
    {
        if (!_congestions.TryGetValue(name, out var value))
        {
            value = new Congestion();
            _congestions.Add(name, value);
        }

        var realTime = Time.RealTime;
        var lastUpdateTime = value.LastUpdateTime;
        var lastPlayedTime = value.LastPlayedTime;
        var num = lastUpdateTime > 0.0 ? (float)(realTime - lastUpdateTime) : 0f;
        value.Value = MathUtils.Max(value.Value - 10f * num, 0f);
        value.LastUpdateTime = realTime;
        if (!(value.Value <= 6f) ||
            (lastPlayedTime != 0.0 && !(volume > value.LastPlayedVolume) && !(realTime - lastPlayedTime >= 0.0)))
        {
            return false;
        }

        value.LastPlayedTime = realTime;
        value.LastPlayedVolume = volume;
        value.Value += 1f;
        return true;
    }

    public class Congestion
    {
        public double LastPlayedTime;

        public float LastPlayedVolume;
        public double LastUpdateTime;

        public float Value;
    }

    public struct SoundInfo
    {
        public double Time;

        public string Name;

        public float Volume;

        public float Pitch;

        public float Pan;
    }
}
