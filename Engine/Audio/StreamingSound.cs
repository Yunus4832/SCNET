using Engine.Core.Mathematics;
using Engine.Media.Audio;

using Silk.NET.OpenAL;

namespace Engine.Audio;

/// <summary>A streaming voice with bounded CPU supply and audio-owner-only native playback.</summary>
public sealed class StreamingSound : BaseSound
{
    private readonly Func<StreamingSource> _open;

    private readonly float _duration;

    private readonly float _startPercentage;

    private AudioStreamReader? _reader;

    private readonly uint[] _buffers = new uint[3];

    private readonly Queue<uint> _available = new();

    private readonly Dictionary<uint, int> _samples = new();

    private long _consumed;

    private long _playedSamples;

    public StreamingSound(
        Func<StreamingSource> open,
        float volume = 1f,
        float pitch = 1f,
        float pan = 0f,
        bool isLooped = false,
        bool disposeOnStop = false,
        float bufferDuration = .3f,
        float startPercentage = 0f,
        CancellationToken cancellation = default
    ) : base(cancellation)
    {
        ArgumentNullException.ThrowIfNull(open);
        _open = open;
        _duration = MathUtils.Clamp(bufferDuration, .01f, 10f);
        _startPercentage = MathUtils.Saturate(startPercentage);
        Volume = volume;
        Pitch = pitch;
        Pan = pan;
        IsLooped = isLooped;
        DisposeOnStop = disposeOnStop;
        Register();
    }

    /// <summary>Samples actually consumed by the native source since this playback began.</summary>
    public long PlayedSamples => Volatile.Read(ref _playedSamples);

    private protected override void UpdatePlayback(AudioService service, AudioPlaybackRequest request)
    {
        var api = service.Api!;
        if (appliedGeneration != request.Generation)
        {
            // Stop audible playback immediately, even while the old decoder is retiring.
            if (source != 0 && ReadNativeState(service) != SourceState.Stopped) { api.SourceStop(source); }

            if (_reader != null)
            {
                _reader.Cancel();
                if (!_reader.Completion.IsCompleted)
                {
                    Preparing(request);
                    return;
                }

                _reader = null;
            }

            Flush(service);
            _consumed = 0;
            Volatile.Write(ref _playedSamples, 0);
            appliedGeneration = request.Generation;
        }

        if (request.State == SoundState.Stopped) { return; }

        _reader ??= new AudioStreamReader(service, _open, request.Looped, _duration, _startPercentage);
        if (!_reader.Ready.IsCompleted)
        {
            Preparing(request);
            return;
        }

        if (_reader.Failure is { } failure)
        {
            throw new InvalidOperationException("Streaming preparation failed.", failure);
        }

        if (_reader.Channels == 0)
        {
            Preparing(request);
            return;
        }

        EnsureSource(service, request, _reader.Channels);
        if (_buffers[0] == 0)
        {
            for (var i = 0; i < _buffers.Length; i++)
            {
                _buffers[i] = api.GenBuffer();
                _available.Enqueue(_buffers[i]);
            }
        }

        api.GetSourceProperty(source, GetSourceInteger.BuffersProcessed, out var processed);
        while (processed-- > 0)
        {
            var buffer = Unqueue(api, source);
            _consumed += _samples.GetValueOrDefault(buffer);
            _available.Enqueue(buffer);
        }

        while (_available.Count > 0 && _reader.TryRead(out var block))
        {
            try
            {
                var buffer = _available.Dequeue();
                unsafe
                {
                    fixed (byte* data = block.Data)
                    {
                        api.BufferData(
                            buffer,
                            _reader.Channels == 1 ? BufferFormat.Mono16 : BufferFormat.Stereo16,
                            data,
                            block.Count,
                            _reader.Frequency
                        );
                    }
                }

                api.SourceQueueBuffers(source, [buffer]);
                _samples[buffer] = block.Count / 2 / _reader.Channels;
            }
            finally { block.Release(); }
        }

        api.GetSourceProperty(source, GetSourceInteger.BuffersQueued, out var queued);
        var state = ReadNativeState(service);
        api.GetSourceProperty(source, GetSourceInteger.SampleOffset, out var offset);
        Volatile.Write(ref _playedSamples, _consumed + Math.Max(0, offset));
        if (request.State == SoundState.Paused)
        {
            if (state == SourceState.Playing) { api.SourcePause(source); }

            Publish(SoundState.Paused, request);
        }
        else if (queued > 0)
        {
            if (!IsCurrent(request)) { return; }

            // Initial preparation and temporary starvation are not end-of-stream.
            if (state != SourceState.Playing)
            {
                api.SourcePlay(source);
                service.CheckError();
            }

            if (ReadNativeState(service) == SourceState.Playing) { Publish(SoundState.Playing, request); }
            else { Preparing(request); }
        }
        else if (_reader.Finished) { Finished(request); }
        else { Preparing(request); }
    }

    private static unsafe uint Unqueue(AL api, uint source)
    {
        uint buffer;
        api.SourceUnqueueBuffers(source, 1, &buffer);
        return buffer;
    }

    private void Flush(AudioService service)
    {
        if (source == 0) { return; }

        var api = service.Api!;
        api.SourceStop(source);
        api.GetSourceProperty(source, GetSourceInteger.BuffersQueued, out var queued);
        while (queued-- > 0) { Unqueue(api, source); }

        _available.Clear();
        foreach (var buffer in _buffers)
        {
            if (buffer != 0) { _available.Enqueue(buffer); }
        }

        _samples.Clear();
    }

    private protected override void ReleaseNative(AudioService service)
    {
        Flush(service);
        foreach (var buffer in _buffers)
        {
            if (buffer != 0) { service.Api!.DeleteBuffer(buffer); }
        }

        base.ReleaseNative(service);
    }

    private protected override Task ReleaseData() => _reader?.Cancel() ?? Task.CompletedTask;
}
