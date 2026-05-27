using Silk.NET.OpenAL;

namespace Engine.Audio;

public sealed class Sound : BaseSound
{
    public SoundBuffer SoundBuffer { get; private set; }

    public Sound(
        SoundBuffer soundBuffer,
        float volume = 1f,
        float pitch = 1f,
        float pan = 0f,
        bool isLooped = false,
        bool disposeOnStop = false
    )
    {
        if (Mixer.IsAudioInitialized)
        {
            Mixer.AL!.SetSourceProperty(Source, SourceInteger.Buffer, soundBuffer.mBuffer);
            Mixer.CheckALError();
        }

        SoundBuffer = soundBuffer;
        ChannelsCount = soundBuffer.ChannelsCount;
        SamplingFrequency = soundBuffer.SamplingFrequency;
        Volume = volume;
        Pitch = pitch;
        Pan = pan;
        IsLooped = isLooped;
        DisposeOnStop = disposeOnStop;
        Mixer.soundsToStopPoll.Add(this);
    }


    public override void Dispose()
    {
        base.Dispose();
        SoundBuffer = null!;
    }

    protected override void InternalPlay()
    {
        if (!Mixer.IsAudioInitialized)
        {
            return;
        }

        if (Mixer.AL is null)
        {
            return;
        }

        Mixer.AL.SetSourceProperty(Source, SourceBoolean.Looping, soundIsLooped);
        Mixer.AL.SourcePlay(Source);
        Mixer.CheckALError();
    }

    protected override void InternalPause()
    {
        if (!Mixer.IsAudioInitialized)
        {
            return;
        }

        if (Mixer.AL is null)
        {
            return;
        }

        Mixer.AL.SourcePause(Source);
        Mixer.CheckALError();
    }

    protected override void InternalStop()
    {
        if (!Mixer.IsAudioInitialized)
        {
            return;
        }

        if (Mixer.AL is null)
        {
            return;
        }

        Mixer.AL.SourceRewind(Source);
        Mixer.CheckALError();
    }

    internal override void InternalDispose()
    {
        if (!Mixer.IsAudioInitialized)
        {
            return;
        }

        base.InternalDispose();
        Mixer.soundsToStopPoll.Remove(this);
    }
}
