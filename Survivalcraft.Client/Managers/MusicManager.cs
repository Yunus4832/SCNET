using Engine.Audio;
using Engine.Media.Audio;

namespace Game.Managers;

public static class MusicManager
{
    public enum Mix
    {
        None = 0,

        Menu = 1
    }

    private const float _fadeSpeed = 0.33f;

    private const float _fadeWait = 2f;

    private static StreamingSound? _fadeSound;

    private static StreamingSound? _sound;

    private static float _targetVolume = float.NaN;

    private static Mix _currentMix;

    private static double _nextSongTime;

    private static readonly Random _sharedRandom = new();

    public static Mix CurrentMix
    {
        get => _currentMix;
        set
        {
            if (value == _currentMix)
            {
                return;
            }

            _currentMix = value;
            _nextSongTime = 0.0;
        }
    }

    public static bool IsPlaying
    {
        get
        {
            if (_sound != null)
            {
                return _sound.State is SoundState.Preparing or SoundState.Playing or SoundState.Paused;
            }

            return false;
        }
    }

    public static float Volume => Configuration.ClientConfiguration.Preferences.MusicVolume * 0.6f;

    public static void Update()
    {
        if (!Mixer.IsAudioInitialized) { return; }

        if (_fadeSound?.State == SoundState.Disposed) { _fadeSound = null; }

        if (_sound != null && _targetVolume != Volume)
        {
            _sound.FadeTo(Volume, Volume > 0 ? MathUtils.Abs(Volume - _sound.Volume) / (_fadeSpeed * Volume) : 0f);
            _targetVolume = Volume;
        }

        if (_currentMix == Mix.None || Volume == 0f)
        {
            StopMusic();
        }
        else if (_currentMix == Mix.Menu && (Time.FrameStartTime >= _nextSongTime || !IsPlaying))
        {
            var startPercentage = IsPlaying ? _sharedRandom.Float(0f, 0.75f) : 0f;
            switch (_sharedRandom.Int(0, 5))
            {
                case 0:
                    PlayMusic("Music/NativeAmericanFluteSpirit", startPercentage);
                    break;
                case 1:
                    PlayMusic("Music/AloneForever", startPercentage);
                    break;
                case 2:
                    PlayMusic("Music/NativeAmerican", startPercentage);
                    break;
                case 3:
                    PlayMusic("Music/NativeAmericanHeart", startPercentage);
                    break;
                case 4:
                    PlayMusic("Music/NativeAmericanPeaceFlute", startPercentage);
                    break;
                case 5:
                    PlayMusic("Music/NativeIndianChant", startPercentage);
                    break;
            }

            _nextSongTime = Time.FrameStartTime + _sharedRandom.Float(40f, 60f);
        }
    }

    public static void PlayMusic(string name, float startPercentage)
    {
        if (string.IsNullOrEmpty(name))
        {
            StopMusic();
        }
        else
        {
            try
            {
                StopMusic();
                var volume = _fadeSound != null ? 0f : Volume;
                var open = AssetManager.CreateStreamFactory(name, "ogg", "wav");
                _sound = new StreamingSound(
                    () => SoundData.Stream(open()),
                    volume,
                    1f,
                    0f,
                    false,
                    false,
                    1f,
                    startPercentage: startPercentage
                );
                _sound.FadeTo(Volume, volume == 0f ? 1f / _fadeSpeed : 0f, _fadeWait);
                _targetVolume = Volume;
                _sound.Play();
            }
            catch (Exception e)
            {
                Log.Warning("Error playing music \"{0}\".{1}", name, e.Message);
            }
        }
    }

    public static void StopMusic()
    {
        if (_sound == null)
        {
            return;
        }

        _fadeSound?.Dispose();
        _fadeSound = _sound;
        _fadeSound.FadeTo(
            0f,
            Volume > 0 ? _fadeSound.Volume / (_fadeSpeed * Volume) : 0f,
            disposeOnCompletion: true
        );
        _sound = null;
    }
}
