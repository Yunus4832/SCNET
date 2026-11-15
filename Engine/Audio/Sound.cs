using Silk.NET.OpenAL;

namespace Engine.Audio;

public sealed class Sound : BaseSound
{
    private readonly SoundBuffer _buffer;

    private bool _acquired;

    private uint _bound;

    public SoundBuffer SoundBuffer => _buffer;

    public Sound(
        SoundBuffer soundBuffer,
        float volume = 1f,
        float pitch = 1f,
        float pan = 0f,
        bool isLooped = false,
        bool disposeOnStop = false,
        CancellationToken cancellation = default
    ) : base(cancellation)
    {
        ArgumentNullException.ThrowIfNull(soundBuffer);
        _buffer = soundBuffer;
        _acquired = _buffer.Acquire();
        Volume = volume;
        Pitch = pitch;
        Pan = pan;
        IsLooped = isLooped;
        DisposeOnStop = disposeOnStop;
        Register();
    }

    private protected override void UpdatePlayback(AudioService service, AudioPlaybackRequest request)
    {
        var api = service.Api!;
        if (!_acquired || _buffer.IsDisposed)
        {
            Dispose();
            return;
        }

        if (request.State == SoundState.Stopped)
        {
            if (source != 0 && appliedGeneration != request.Generation) { api.SourceRewind(source); }

            appliedGeneration = request.Generation;
            return;
        }

        _buffer.TryPrepare(service);
        _buffer.ThrowIfFailed();
        if (!_buffer.IsReady)
        {
            Preparing(request);
            return;
        }

        EnsureSource(service, request, _buffer.ChannelsCount);
        if (_bound == 0)
        {
            _bound = service.GetBuffer(_buffer);
            api.SetSourceProperty(source, SourceInteger.Buffer, _bound);
        }

        if (request.State == SoundState.Paused)
        {
            api.SourcePause(source);
            Publish(SoundState.Paused, request);
            return;
        }

        if (!IsCurrent(request)) { return; }

        if (appliedGeneration != request.Generation)
        {
            api.SetSourceProperty(source, SourceBoolean.Looping, request.Looped);
            api.SourceRewind(source);
            api.SourcePlay(source);
            service.CheckError();
            appliedGeneration = request.Generation;
        }
        else if (ReadNativeState(service) == SourceState.Paused) { api.SourcePlay(source); }

        if (ReadNativeState(service) == SourceState.Stopped) { Finished(request); }
        else { Publish(SoundState.Playing, request); }
    }

    private protected override Task ReleaseData()
    {
        if (_acquired)
        {
            _acquired = false;
            _buffer.Release();
        }

        return Task.CompletedTask;
    }
}
