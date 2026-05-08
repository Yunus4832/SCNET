using Engine.Core;
using Silk.NET.OpenAL;

namespace Engine.Audio;

public abstract class BaseSound : IDisposable
{
    private bool _disposeOnStop;

    protected bool soundIsLooped;

    protected readonly Lock stateSync = new();

    public uint MSource;

    internal BaseSound()
    {
        if (!Mixer.IsAudioInitialized)
        {
            return;
        }

        if (Mixer.AL is null)
        {
            return;
        }

        MSource = Mixer.AL.GenSource();
        Mixer.CheckALError();
        Mixer.AL.DistanceModel(DistanceModel.None);
        Mixer.CheckALError();
    }

    public SoundState State { get; private set; }

    protected int ChannelsCount { get; init; }

    protected int SamplingFrequency { get; init; }

    public float Volume
    {
        get;
        set
        {
            value = MathUtils.Saturate(value);
            if (value.CloseTo(field))
            {
                return;
            }

            InternalSetVolume(value);
            field = value;
        }
    } = 1f;

    public float Pitch
    {
        get;
        set
        {
            value = MathUtils.Clamp(value, 0.5f, 2f);
            if (value.CloseTo(field))
            {
                return;
            }

            InternalSetPitch(value);
            field = value;
        }
    } = 1f;

    public float Pan
    {
        get;
        set
        {
            if (ChannelsCount != 1)
            {
                return;
            }

            value = MathUtils.Clamp(value, -1f, 1f);
            if (value.CloseTo(field))
            {
                return;
            }

            InternalSetPan(value);
            field = value;
        }
    }

    public bool IsLooped
    {
        get => soundIsLooped;
        set
        {
            lock (stateSync)
            {
                if (State == SoundState.Stopped)
                {
                    soundIsLooped = value;
                }
            }
        }
    }

    public bool DisposeOnStop
    {
        get => _disposeOnStop;
        set
        {
            lock (stateSync)
            {
                if (State == SoundState.Stopped)
                {
                    _disposeOnStop = value;
                }
            }
        }
    }

    public virtual void Dispose()
    {
        if (State == SoundState.Disposed)
        {
            return;
        }

        State = SoundState.Disposed;
        InternalDispose();
    }

    public void Play()
    {
        lock (stateSync)
        {
            if (State != SoundState.Stopped && State != SoundState.Paused)
            {
                return;
            }

            State = SoundState.Playing;
            InternalPlay();
        }
    }

    public void Pause()
    {
        lock (stateSync)
        {
            if (State != SoundState.Playing)
            {
                return;
            }

            State = SoundState.Paused;
            InternalPause();
        }
    }

    public void Stop()
    {
        if (_disposeOnStop)
        {
            Dispose();
        }

        lock (stateSync)
        {
            if (State is not (SoundState.Playing or SoundState.Paused))
            {
                return;
            }

            State = SoundState.Stopped;
            InternalStop();
        }
    }

    public static void CalculateStereoVolumes(float volume, float pan, out float left, out float right)
    {
        left = volume * MathUtils.Saturate(0f - pan + 1f);
        right = volume * MathUtils.Saturate(pan + 1f);
    }

    private void InternalSetVolume(float volume)
    {
        if (MSource == 0)
        {
            return;
        }

        if (Mixer.AL is null)
        {
            return;
        }

        Mixer.AL.SetSourceProperty(MSource, SourceFloat.Gain, volume);
        Mixer.CheckALError();
    }

    private void InternalSetPitch(float pitch)
    {
        if (MSource == 0)
        {
            return;
        }

        if (Mixer.AL is null)
        {
            return;
        }

        Mixer.AL.SetSourceProperty(MSource, SourceFloat.Pitch, pitch);
        Mixer.CheckALError();
    }

    private void InternalSetPan(float pan)
    {
        if (MSource == 0)
        {
            return;
        }

        if (Mixer.AL is null)
        {
            return;
        }

        const float value = 0f;
        const float value2 = -0.1f;
        Mixer.AL.SetSourceProperty(MSource, SourceVector3.Position, pan, value, value2);
        Mixer.CheckALError();
    }

    protected abstract void InternalPlay();

    protected abstract void InternalPause();

    protected abstract void InternalStop();

    internal virtual void InternalDispose()
    {
        if (MSource == 0)
        {
            return;
        }

        if (Mixer.AL is null)
        {
            return;
        }

        Mixer.AL.SourceStop(MSource);
        Mixer.CheckALError();
        Mixer.AL.DeleteSource(MSource);
        Mixer.CheckALError();
        MSource = 0;
    }
}
