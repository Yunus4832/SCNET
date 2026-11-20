using System.Diagnostics;

using Engine.Core.Logging;
using Engine.Core.Mathematics;

using Silk.NET.OpenAL;

namespace Engine.Audio;

/// <summary>A coalesced playback intent; native state belongs exclusively to the audio service.</summary>
public abstract class BaseSound : IDisposable, IAsyncDisposable
{
    private readonly Lock _sync = new();

    private readonly CancellationToken _cancellation;

    private SoundState _desired;

    private int _state;

    private long _revision;

    private long _generation;

    private float _volume = 1f;

    private float _pitch = 1f;

    private float _pan;

    private bool _looped;

    private bool _disposeOnStop;

    private bool _retired;

    private sealed record VolumeFade(float Target, float Seconds, float Delay, bool DisposeOnCompletion);

    private VolumeFade? _fade;

    private VolumeFade? _activeFade;

    private double _fadeStarted;

    private float _fadeFrom;

    private TaskCompletionSource? _completion;

    private AudioService? _service;

    internal uint source;

    internal int nativeState;

    protected long appliedGeneration = -1;

    private AudioPlaybackRequest? _applied;

    protected BaseSound(CancellationToken cancellation = default)
    {
        _cancellation = cancellation;
    }

    public SoundState State => (SoundState)Volatile.Read(ref _state);

    public float Volume
    {
        get
        {
            lock (_sync) { return _volume; }
        }
        set
        {
            value = MathUtils.Saturate(value);
            lock (_sync)
            {
                if (_volume.CloseTo(value) && _fade == null) { return; }

                _volume = value;
                _fade = null;
                _revision++;
            }

            _service?.Wake();
        }
    }

    public float Pitch
    {
        get
        {
            lock (_sync) { return _pitch; }
        }
        set
        {
            value = MathUtils.Clamp(value, .5f, 2f);
            lock (_sync)
            {
                if (_pitch.CloseTo(value)) { return; }

                _pitch = value;
                _revision++;
            }

            _service?.Wake();
        }
    }

    public float Pan
    {
        get
        {
            lock (_sync) { return _pan; }
        }
        set
        {
            value = MathUtils.Clamp(value, -1f, 1f);
            lock (_sync)
            {
                if (_pan.CloseTo(value)) { return; }

                _pan = value;
                _revision++;
            }

            _service?.Wake();
        }
    }

    public bool IsLooped
    {
        get
        {
            lock (_sync) { return _looped; }
        }
        set
        {
            lock (_sync)
            {
                if (_desired == SoundState.Stopped)
                {
                    _looped = value;
                    _revision++;
                }
            }
        }
    }

    public bool DisposeOnStop
    {
        get
        {
            lock (_sync) { return _disposeOnStop; }
        }
        set
        {
            lock (_sync)
            {
                if (_desired == SoundState.Stopped)
                {
                    _disposeOnStop = value;
                    _revision++;
                }
            }
        }
    }

    protected void Register()
    {
        _service = Mixer.Service;
        if (_service?.Register(this) != true)
        {
            Dispose();
            RetireWithoutNative();
        }
    }

    public void FadeTo(float volume, float seconds, float delay = 0f, bool disposeOnCompletion = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(seconds);
        ArgumentOutOfRangeException.ThrowIfNegative(delay);
        if (!float.IsFinite(seconds) || !float.IsFinite(delay))
        {
            throw new ArgumentOutOfRangeException(nameof(seconds));
        }

        lock (_sync)
        {
            if (_desired == SoundState.Disposed) { return; }

            _fade = new VolumeFade(MathUtils.Saturate(volume), seconds, delay, disposeOnCompletion);
            _revision++;
        }

        _service?.Wake();
    }

    public void Play()
    {
        lock (_sync)
        {
            if (_desired is not (SoundState.Stopped or SoundState.Paused)) { return; }

            if (_desired == SoundState.Stopped) { _generation++; }

            _desired = SoundState.Playing;
            _revision++;
            Volatile.Write(ref _state, (int)SoundState.Preparing);
        }

        _service?.Wake();
    }

    public void Pause()
    {
        lock (_sync)
        {
            if (_desired != SoundState.Playing) { return; }

            _desired = SoundState.Paused;
            _revision++;
            Volatile.Write(ref _state, (int)SoundState.Paused);
        }

        _service?.Wake();
    }

    public void Stop()
    {
        lock (_sync)
        {
            if (_desired == SoundState.Disposed) { return; }

            _desired = _disposeOnStop ? SoundState.Disposed : SoundState.Stopped;
            _fade = null;
            _generation++;
            _revision++;
            Volatile.Write(ref _state, (int)_desired);
        }

        _service?.Wake();
    }

    public virtual void Dispose()
    {
        lock (_sync)
        {
            _desired = SoundState.Disposed;
            _revision++;
            Volatile.Write(ref _state, (int)SoundState.Disposed);
        }

        _service?.Wake();
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        lock (_sync)
        {
            if (_retired) { return ValueTask.CompletedTask; }

            _completion ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            return new ValueTask(_completion.Task);
        }
    }

    internal bool Update(AudioService service)
    {
        AudioPlaybackRequest request;
        lock (_sync)
        {
            if (_cancellation.IsCancellationRequested)
            {
                _desired = SoundState.Disposed;
                Volatile.Write(ref _state, (int)SoundState.Disposed);
            }

            AdvanceFade();
            request = new(_desired, _revision, _generation, _volume, _pitch, _pan, _looped, _disposeOnStop);
        }

        if (request.State == SoundState.Disposed)
        {
            Retire(service);
            return false;
        }

        try { UpdatePlayback(service, request); }
        catch (Exception error)
        {
            Log.Warning($"Audio playback failed: {error}");
            Dispose();
            Retire(service);
            return false;
        }

        return true;
    }

    private void AdvanceFade()
    {
        if (_fade is not { } fade)
        {
            _activeFade = null;
            return;
        }

        var now = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
        if (!ReferenceEquals(_activeFade, fade))
        {
            _activeFade = fade;
            _fadeFrom = _volume;
            _fadeStarted = now + fade.Delay;
        }

        if (now < _fadeStarted) { return; }

        var amount = fade.Seconds == 0 ? 1f : MathUtils.Saturate((float)((now - _fadeStarted) / fade.Seconds));
        _volume = MathUtils.Lerp(_fadeFrom, fade.Target, amount);
        if (amount < 1f) { return; }

        _fade = null;
        _activeFade = null;
        if (fade.DisposeOnCompletion)
        {
            _desired = SoundState.Disposed;
            _revision++;
            Volatile.Write(ref _state, (int)SoundState.Disposed);
        }
    }

    private protected abstract void UpdatePlayback(AudioService service, AudioPlaybackRequest request);

    private protected virtual Task ReleaseData() => Task.CompletedTask;

    private protected virtual void ReleaseNative(AudioService service)
    {
        if (source == 0) { return; }

        service.Api!.SourceStop(source);
        service.Api.DeleteSource(source);
        source = 0;
    }

    private protected void EnsureSource(AudioService service, AudioPlaybackRequest request, int channels)
    {
        var api = service.Api!;
        if (source == 0)
        {
            source = api.GenSource();
            _applied = null;
        }

        if (source == 0) { throw new InvalidOperationException("No audio source is available."); }

        if (_applied is not { } previous || previous.Volume != request.Volume)
        {
            api.SetSourceProperty(source, SourceFloat.Gain, request.Volume);
        }

        if (_applied is not { } pitch || pitch.Pitch != request.Pitch)
        {
            api.SetSourceProperty(source, SourceFloat.Pitch, request.Pitch);
        }

        if (channels == 1 && (_applied is not { } pan || pan.Pan != request.Pan))
        {
            api.SetSourceProperty(source, SourceVector3.Position, request.Pan, 0f, -.1f);
        }

        _applied = request;
    }

    private protected SourceState ReadNativeState(AudioService service)
    {
        service.Api!.GetSourceProperty(source, GetSourceInteger.SourceState, out var state);
        Volatile.Write(ref nativeState, state);
        return (SourceState)state;
    }

    private protected bool IsCurrent(AudioPlaybackRequest request)
    {
        lock (_sync)
        {
            return _revision == request.Revision && _desired == request.State && !_cancellation.IsCancellationRequested;
        }
    }

    private protected void Preparing(AudioPlaybackRequest request)
    {
        Publish(request.State == SoundState.Playing ? SoundState.Preparing : request.State, request);
    }

    private protected void Publish(SoundState state, AudioPlaybackRequest request)
    {
        lock (_sync)
        {
            if (_revision == request.Revision && _desired == request.State)
            {
                Volatile.Write(ref _state, (int)state);
            }
        }
    }

    private protected void Finished(AudioPlaybackRequest request)
    {
        lock (_sync)
        {
            if (_revision != request.Revision) { return; }

            _desired = _disposeOnStop ? SoundState.Disposed : SoundState.Stopped;
            Volatile.Write(ref _state, (int)_desired);
        }
    }

    private void Retire(AudioService service)
    {
        ReleaseNative(service);
        service.TrackCleanup(this, ReleaseData(), MarkRetired);
    }

    private void RetireWithoutNative()
    {
        var cleanup = ReleaseData();
        _ = cleanup.ContinueWith(
            _ => MarkRetired(),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default
        );
    }

    private void MarkRetired()
    {
        lock (_sync)
        {
            _retired = true;
            _completion?.TrySetResult();
        }
    }

    public static void CalculateStereoVolumes(float volume, float pan, out float left, out float right)
    {
        left = volume * MathUtils.Saturate(1f - pan);
        right = volume * MathUtils.Saturate(1f + pan);
    }
}
