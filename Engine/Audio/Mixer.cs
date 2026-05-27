using Engine.Core;

using Silk.NET.OpenAL;

namespace Engine.Audio;

public static class Mixer
{
    private static readonly List<BaseSound> _soundsToStop = [];

    internal static readonly HashSet<BaseSound> soundsToStopPoll = [];

    internal static ALContext? audioContext;

    public static AL? AL;

    private static nint _device;

    private static nint _context;

    public static bool IsAudioInitialized {
        get;
        private set;
    }

    public static float MasterVolume
    {
        get;
        set
        {
            value = MathUtils.Saturate(value);
            if (value.CloseTo(field))
            {
                return;
            }

            field = value;
            InternalSetMasterVolume(value);
        }
    } = 1f;

    public static void Initialize()
    {
        IsAudioInitialized = false;
        try
        {
            if (audioContext != null)
            {
                return;
            }

            audioContext = ALContext.GetApi();
            AL = AL.GetApi();
            unsafe
            {
                var device = audioContext.OpenDevice("");
                if (device is null)
                {
                    Log.Error("Could not create audio device");
                    return;
                }

                var c = audioContext.CreateContext(device, null);
                audioContext.MakeContextCurrent(c);
                _device = (nint)device;
                _context = (nint)c;
            }

            if (!CheckAllErrorFull())
            {
                IsAudioInitialized = true;
            }
        }
        catch(Exception e)
        {
            Log.Error($"OpenAL Audio is unsupported in this device, {e}");
        }
    }

    internal static void Dispose()
    {
        if (audioContext == null)
        {
            return;
        }

        var soundsSnapshot = new BaseSound[soundsToStopPoll.Count];
        soundsToStopPoll.CopyTo(soundsSnapshot);
        foreach (var sound in soundsSnapshot)
        {
            sound.Dispose();
        }

        foreach (var sound in _soundsToStop)
        {
            sound.Dispose();
        }

        soundsToStopPoll.Clear();
        _soundsToStop.Clear();

        unsafe
        {
            if (_context != 0)
            {
                audioContext.MakeContextCurrent((Context*)0);
                audioContext.DestroyContext((Context*)_context);
                _context = 0;
            }

            if (_device != 0)
            {
                audioContext.CloseDevice((Device*)_device);
                _device = 0;
            }
        }

        IsAudioInitialized = false;
        AL = null;
        audioContext = null;
    }

    internal static void BeforeFrame()
    {
        if (AL is null)
        {
            return;
        }

        var soundsSnapshot = new BaseSound[soundsToStopPoll.Count];
        soundsToStopPoll.CopyTo(soundsSnapshot);
        foreach (var item in soundsSnapshot)
        {
            AL.GetSourceProperty(item.Source, GetSourceInteger.SourceState, out var sourceState);
            var flag = (SourceState)sourceState == SourceState.Stopped;

            if (item.State == SoundState.Playing && flag)
            {
                _soundsToStop.Add(item);
            }
        }

        foreach (var item2 in _soundsToStop)
        {
            item2.Stop();
        }

        _soundsToStop.Clear();
    }

    internal static void AfterFrame()
    {
    }

    internal static void InternalSetMasterVolume(float volume)
    {
        if (AL is null)
        {
            return;
        }

        if (IsAudioInitialized)
        {
            AL.SetListenerProperty(ListenerFloat.Gain, volume);
        }
    }

    public static AudioError CheckALError()
    {
        if (AL is null)
        {
            return AudioError.InvalidValue;
        }

        var error = AL.GetError();
        if (error != AudioError.NoError)
        {
            Log.Error($"OpenAL error: {error}");
        }

        return error;
    }

    public static bool CheckAllErrorFull()
    {
        try
        {
            if (AL is null)
            {
                return false;
            }

            var error = AL.GetError();
            if (error == AudioError.NoError)
            {
                return false;
            }

            Log.Error($"Openal error: {error}");
            return true;
        }
        catch (Exception e)
        {
            Log.Error($"Unable to load OPENAL: {e}");
            return true;
        }
    }
}
