using System.Buffers;
using System.Threading.Channels;

using Engine.Media.Audio;

namespace Engine.Audio;

/// <summary>在 CPU 上生产音频数据，自行管理输入流，不访问原生设备。</summary>
internal sealed class AudioStreamReader
{
    private readonly CancellationTokenSource _cancellation = new();

    private readonly Lock _cancelSync = new();

    private bool _completed;

    private Task? _retirement;

    private readonly Channel<AudioPcmBlock> _blocks = Channel.CreateBounded<AudioPcmBlock>(
        new BoundedChannelOptions(3)
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.Wait,
            AllowSynchronousContinuations = false
        }
    );

    private readonly AudioService _service;

    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public AudioStreamReader(
        AudioService service,
        Func<StreamingSource> open,
        bool looped,
        float duration,
        float startPercentage
    )
    {
        _service = service;
        Completion = Task.Run(() => Run(open, looped, duration, startPercentage));
    }

    public Task Completion { get; }

    public Task Ready => _ready.Task;

    public int Channels { get; private set; }

    public int Frequency { get; private set; }

    public bool Finished => _blocks.Reader.Completion.IsCompleted;

    public Exception? Failure { get; private set; }

    public bool TryRead(out AudioPcmBlock block) => _blocks.Reader.TryRead(out block);

    public Task Cancel()
    {
        lock (_cancelSync)
        {
            if (!_completed) { _cancellation.Cancel(); }

            return _retirement ??= DrainAfterCompletion();
        }
    }

    private async Task DrainAfterCompletion()
    {
        try { await Completion.ConfigureAwait(false); }
        finally
        {
            while (_blocks.Reader.TryRead(out var block)) { block.Release(); }
        }
    }

    private async Task Run(Func<StreamingSource> open, bool looped, float duration, float startPercentage)
    {
        StreamingSource? source = null;
        var token = _cancellation.Token;
        try
        {
            source = await _service.Preparation.Submit(open, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            Channels = source.ChannelsCount;
            Frequency = source.SamplingFrequency;
            if (Channels is < 1 or > 2 || Frequency is < 8000 or > 48000 || source.BytesCount <= 0)
            {
                throw new InvalidOperationException("Invalid streaming audio format.");
            }

            var stream = source;
            await _service.Preparation.Submit(
                () =>
                {
                    stream.Position = (long)(startPercentage * stream.BytesCount / stream.ChannelsCount / 2) / 16 * 16;
                    return true;
                },
                token
            ).ConfigureAwait(false);
            _ready.TrySetResult();
            _service.Wake();
            var size = Math.Max(2 * Channels, 2 * Channels * (int)(Frequency * duration / 3));
            while (!token.IsCancellationRequested)
            {
                var bytes = ArrayPool<byte>.Shared.Rent(size);
                var transferred = false;
                try
                {
                    var read = await _service.Preparation.Submit(() => Read(stream, bytes, size, looped), token)
                        .ConfigureAwait(false);
                    token.ThrowIfCancellationRequested();
                    if (read == 0) { break; }

                    await _blocks.Writer.WriteAsync(new AudioPcmBlock(bytes, read), token).ConfigureAwait(false);
                    transferred = true;
                    _service.Wake();
                    if (read < size) { break; }
                }
                finally
                {
                    if (!transferred) { ArrayPool<byte>.Shared.Return(bytes); }
                }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error) { Failure = error; }
        finally
        {
            try { source?.Dispose(); }
            catch (Exception error) { Failure ??= error; }

            _ready.TrySetResult();
            _blocks.Writer.TryComplete();
            lock (_cancelSync)
            {
                _completed = true;
                _cancellation.Dispose();
            }

            _service.Wake();
        }
    }

    private static int Read(StreamingSource source, byte[] data, int length, bool looped)
    {
        var count = 0;
        var rewound = false;
        while (count < length)
        {
            var read = source.Read(data, count, length - count);
            if (read > 0)
            {
                count += read;
                rewound = false;
                continue;
            }

            if (!looped || rewound) { break; }

            source.Position = 0;
            rewound = true;
        }

        return count;
    }
}
