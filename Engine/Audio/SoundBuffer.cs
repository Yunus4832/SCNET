using Engine.Core.FileStorage;
using Engine.Media.Audio;

namespace Engine.Audio;

/// <summary>Owned decoded PCM; native buffers are created and retired by the audio service.</summary>
public sealed class SoundBuffer : IDisposable
{
    private readonly Lock _sync = new();

    private Task<SoundData>? _data;

    private readonly Func<SoundData>? _prepare;

    private readonly CancellationTokenSource _cancellation = new();

    private int _uses;

    private bool _disposed;

    public SoundBuffer(byte[] data, int startIndex, int itemsCount, int channelsCount, int samplingFrequency)
        : this(Copy(data, startIndex, itemsCount, channelsCount, samplingFrequency))
    {
    }

    public SoundBuffer(short[] data, int startIndex, int itemsCount, int channelsCount, int samplingFrequency)
        : this(Copy(data, startIndex, itemsCount, channelsCount, samplingFrequency))
    {
    }

    public SoundBuffer(Stream stream, int bytesCount, int channelsCount, int samplingFrequency)
        : this(Read(stream, bytesCount, channelsCount, samplingFrequency))
    {
    }

    private SoundBuffer(SoundData data)
    {
        Validate(data);
        _data = Task.FromResult(data);
    }

    private SoundBuffer(Func<Stream> open)
    {
        _prepare = () =>
        {
            using var source = open();
            var data = SoundData.Load(source);
            Validate(data);
            return data;
        };
    }

    internal void TryPrepare(AudioService service)
    {
        lock (_sync)
        {
            if (_disposed || _data != null) { return; }

            if (!service.Preparation.TrySubmit(_prepare!, _cancellation.Token, out var task)) { return; }

            _data = task!;
            _ = task!.ContinueWith(
                completed =>
                {
                    _ = completed.Exception;
                    service.Wake();
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default
            );
        }
    }

    public int ChannelsCount => Data.ChannelsCount;

    public int SamplingFrequency => Data.SamplingFrequency;

    public int SamplesCount => Data.Data.Length / Data.ChannelsCount;

    public int UseCount => Volatile.Read(ref _uses);

    internal bool IsDisposed
    {
        get
        {
            lock (_sync) { return _disposed; }
        }
    }

    internal bool IsReady => Volatile.Read(ref _data)?.IsCompletedSuccessfully == true;

    internal SoundData Data => Volatile.Read(ref _data) is { IsCompletedSuccessfully: true } data
        ? data.Result
        : throw new InvalidOperationException("Audio data has not been prepared.");

    public async ValueTask PrepareAsync()
    {
        while (!IsDisposed)
        {
            if (Volatile.Read(ref _data) is { } prepared)
            {
                await prepared.ConfigureAwait(false);
                return;
            }

            TryPrepare(Mixer.Service ?? throw new InvalidOperationException("Audio service is not initialized."));
            if (Volatile.Read(ref _data) is { } data)
            {
                await data.ConfigureAwait(false);
                return;
            }

            await Task.Delay(2).ConfigureAwait(false);
        }

        throw new ObjectDisposedException(nameof(SoundBuffer));
    }

    public static SoundBuffer LoadDeferred(Func<Stream> open)
    {
        ArgumentNullException.ThrowIfNull(open);
        return new SoundBuffer(open);
    }

    internal bool Acquire()
    {
        lock (_sync)
        {
            if (_disposed) { return false; }

            _uses++;
            return true;
        }
    }

    internal void Release()
    {
        lock (_sync) { _uses--; }

        Mixer.Service?.Wake();
    }

    internal void ThrowIfFailed()
    {
        if (Volatile.Read(ref _data) is { } data && (data.IsFaulted || data.IsCanceled))
        {
            data.GetAwaiter().GetResult();
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed) { return; }

            _disposed = true;
        }

        _cancellation.Cancel();
        if (Volatile.Read(ref _data) is { } task)
        {
            _ = task.ContinueWith(
                _ => _cancellation.Dispose(),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default
            );
        }
        else { _cancellation.Dispose(); }

        Mixer.Service?.Wake();
    }

    public static SoundBuffer Load(SoundData data) =>
        new(data.Data, 0, data.Data.Length, data.ChannelsCount, data.SamplingFrequency);

    public static SoundBuffer Load(Stream stream, SoundFileFormat format) => new(SoundData.Load(stream, format));

    public static SoundBuffer Load(Stream stream) => new(SoundData.Load(stream));

    public static SoundBuffer Load(string fileName, SoundFileFormat format)
    {
        using var stream = Storage.OpenFile(fileName, OpenFileMode.Read);
        return Load(stream, format);
    }

    public static SoundBuffer Load(string fileName)
    {
        using var stream = Storage.OpenFile(fileName, OpenFileMode.Read);
        return Load(stream);
    }

    private static void Validate(SoundData data)
    {
        if (data.Data.Length == 0 || data.SamplingFrequency > 48000)
        {
            throw new InvalidOperationException("Invalid audio sample count or sampling frequency.");
        }
    }

    private static SoundData Copy(byte[] data, int start, int count, int channels, int rate)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (start > data.Length - count) { throw new ArgumentOutOfRangeException(nameof(count)); }

        var result = new SoundData(channels, rate, count);
        Buffer.BlockCopy(data, start, result.Data, 0, count);
        return result;
    }

    private static SoundData Copy(short[] data, int start, int count, int channels, int rate)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (start > data.Length - count) { throw new ArgumentOutOfRangeException(nameof(count)); }

        var result = new SoundData(channels, rate, checked(count * 2));
        data.AsSpan(start, count).CopyTo(result.Data);
        return result;
    }

    private static SoundData Read(Stream stream, int bytes, int channels, int rate)
    {
        var data = new byte[bytes];
        stream.ReadExactly(data);
        return Copy(data, 0, bytes, channels, rate);
    }
}
