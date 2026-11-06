using Engine.Audio;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems;

public class SubsystemAudio : Subsystem, IUpdateable
{
    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Presentation;

    private readonly Dictionary<string, Congestion> _congestions = new();

    private readonly List<Vector3> _listenerPositions = [];

    private readonly Dictionary<Sound, bool> _mutedSounds = new();

    private double _nextSoundTime;

    private readonly List<SoundInfo> _queuedSounds = [];

    private readonly Random _random = new();

    private SubsystemSpatialSoundRequests? _requests;

    private readonly Dictionary<string, List<SpatialSoundRequest>> _nearest = new(StringComparer.Ordinal);

    private readonly List<Sound> _sounds = [];

    private SubsystemTime _subsystemTime = null!;

    private SubsystemGameWidgets _subsystemViews = null!;

    public ReadOnlyList<Vector3> ListenerPositions => new(_listenerPositions);

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        if (!WorldRuntimeContext.For(Project).HasPresentation) { return; }

        _listenerPositions.Clear();
        foreach (var gameWidget in _subsystemViews.GameWidgets)
        {
            if (gameWidget.CharacterData.IsLocalCharacter)
            {
                _listenerPositions.Add(gameWidget.ActiveCamera.ViewPosition);
            }
        }

        FlushNearestRequests();

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
                if (_subsystemTime.GameTimeFactor.CloseTo(1f) &&
                    !_subsystemTime.FixedTimeStep.HasValue &&
                    soundInfo.Volume * Configuration.ClientConfiguration.Preferences.SoundsVolume >
                    AudioManager.MinAudibleVolume &&
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
        if (!WorldRuntimeContext.For(Project).HasPresentation) { return; }

        var num = _subsystemTime.GameTime + delay;
        _nextSoundTime = MathUtils.Min(_nextSoundTime, num);
        _queuedSounds.Add(
            new SoundInfo
            {
                Time = num,
                Name = name,
                Volume = volume,
                Pitch = pitch,
                Pan = pan
            }
        );
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
        if (!WorldRuntimeContext.For(Project).HasPresentation) { return; }

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

    public void PlayRandomSound(
        string directory,
        float volume,
        float pitch,
        Vector3 position,
        float minDistance,
        float delay
    )
    {
        var num = CalculateVolume(CalculateListenerDistance(position), minDistance);
        PlayRandomSound(directory, volume * num, pitch, 0f, delay);
    }

    public void PlayRandomSound(
        string directory,
        float volume,
        float pitch,
        Vector3 position,
        float minDistance,
        bool autoDelay
    )
    {
        var num = CalculateVolume(CalculateListenerDistance(position), minDistance);
        PlayRandomSound(directory, volume * num, pitch, 0f, autoDelay ? CalculateDelay(position) : 0f);
    }

    public Sound CreateSound(string name)
    {
        if (!WorldRuntimeContext.For(Project).HasPresentation)
        {
            throw new InvalidOperationException("Only a presentation world can create audio resources.");
        }

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
        if (!WorldRuntimeContext.For(Project).HasPresentation) { return; }

        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _subsystemViews = Project.FindSubsystem<SubsystemGameWidgets>(true)!;
        _requests = Project.FindSubsystem<SubsystemSpatialSoundRequests>(true)!;
        _requests.Requested += OnSpatialSoundRequested;
    }

    public override void Dispose()
    {
        if (_requests is not null) { _requests.Requested -= OnSpatialSoundRequested; }

        _requests = null;
        _nearest.Clear();
        _queuedSounds.Clear();
        foreach (var sound in _sounds)
        {
            sound.Dispose();
        }

        _sounds.Clear();
        _mutedSounds.Clear();
        _listenerPositions.Clear();
    }

    private void OnSpatialSoundRequested(SpatialSoundRequest request)
    {
        if (!WorldRuntimeContext.IsProjectActive(Project)) { return; }

        if (request.NearestGroup is { } group)
        {
            if (!_nearest.TryGetValue(group, out var requests))
            {
                requests = [];
                _nearest.Add(group, requests);
            }

            requests.Add(request);
        }
        else { PlaySpatialRequest(request); }
    }

    private void FlushNearestRequests()
    {
        foreach (var requests in _nearest.Values)
        {
            SpatialSoundRequest? nearest = null;
            var distance = float.MaxValue;
            foreach (var request in requests)
            {
                var candidate = CalculateListenerDistanceSquared(request.Position);
                if (nearest is null || candidate < distance)
                {
                    nearest = request;
                    distance = candidate;
                }
            }

            if (nearest is { } selected) { PlaySpatialRequest(selected); }
        }

        _nearest.Clear();
    }

    private void PlaySpatialRequest(SpatialSoundRequest request)
    {
        var pitch = request.PitchVariation > 0f
            ? request.Pitch + _random.Float(-request.PitchVariation, request.PitchVariation)
            : request.Pitch;
        if (request.RandomDirectory)
        {
            if (request.AutoDelay)
            {
                PlayRandomSound(request.Asset, request.Volume, pitch, request.Position, request.MinimumDistance, true);
            }
            else
            {
                PlayRandomSound(
                    request.Asset,
                    request.Volume,
                    pitch,
                    request.Position,
                    request.MinimumDistance,
                    request.DelaySeconds
                );
            }
        }
        else
        {
            if (request.AutoDelay)
            {
                PlaySound(request.Asset, request.Volume, pitch, request.Position, request.MinimumDistance, true);
            }
            else
            {
                PlaySound(
                    request.Asset,
                    request.Volume,
                    pitch,
                    request.Position,
                    request.MinimumDistance,
                    request.DelaySeconds
                );
            }
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
