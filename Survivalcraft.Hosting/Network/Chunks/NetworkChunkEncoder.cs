using System.Collections.Concurrent;
using System.Threading.Channels;

using Game.Terrains.Distribution;

namespace Game.Network.Chunks;

public sealed class NetworkChunkEncoder : IDisposable
{
    private readonly ConcurrentQueue<EncodingCompletion> _completions = new();

    private readonly Func<AuthorityChunkSnapshot, CancellationToken, EncodedTerrainChunk> _encode;

    private readonly Lock _lock = new();

    private readonly HashSet<AuthorityChunkDescriptor> _pending = [];

    private readonly Channel<AuthorityChunkSnapshot> _queue;

    private readonly int _maximumOutstanding;

    private readonly Task _worker;

    private readonly CancellationTokenSource _stopping = new();

    private bool _disposed;

    public NetworkChunkEncoder(
        int maximumOutstanding = NetworkTerrainPolicy.DefaultServerChunkCountSendPer,
        Func<AuthorityChunkSnapshot, CancellationToken, EncodedTerrainChunk>? encode = null
    )
    {
        if (maximumOutstanding <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumOutstanding));
        }

        _maximumOutstanding = maximumOutstanding;
        _encode = encode ?? NetworkChunkCodec.Encode;
        _queue = Channel.CreateBounded<AuthorityChunkSnapshot>(
            new BoundedChannelOptions(maximumOutstanding)
            {
                AllowSynchronousContinuations = false,
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = true
            }
        );
        _worker = Task.Run(WorkerLoop);
    }

    public int OutstandingCount
    {
        get
        {
            lock (_lock)
            {
                return _pending.Count;
            }
        }
    }

    public bool CanSchedule(AuthorityChunkDescriptor descriptor)
    {
        lock (_lock)
        {
            return !_disposed && _pending.Count < _maximumOutstanding && !_pending.Contains(descriptor);
        }
    }

    public IReadOnlyList<EncodedTerrainChunk> DrainCompleted(
        NetworkChunkCache cache,
        Func<AuthorityChunkDescriptor, bool>? accept = null
    )
    {
        ArgumentNullException.ThrowIfNull(cache);
        List<EncodedTerrainChunk>? ready = null;
        while (_completions.TryDequeue(out var completion))
        {
            lock (_lock)
            {
                _pending.Remove(completion.Descriptor);
            }

            if (accept != null && !accept(completion.Descriptor)) { continue; }

            if (completion.Error != null)
            {
                Log.Warning(
                    $"Failed to encode terrain chunk {completion.Descriptor.Coords}: " +
                    $"{completion.Error.GetType().Name}: {completion.Error.Message}"
                );
                continue;
            }

            if (completion.Encoded != null)
            {
                cache.Store(completion.Encoded);
                (ready ??= []).Add(completion.Encoded);
            }
        }

        return ready is null ? Array.Empty<EncodedTerrainChunk>() : ready;
    }

    public bool TrySchedule(AuthorityChunkSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var descriptor = new AuthorityChunkDescriptor(snapshot.Coords, snapshot.ContentVersion);
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_pending.Contains(descriptor))
            {
                return true;
            }

            if (_pending.Count >= _maximumOutstanding)
            {
                return false;
            }

            _pending.Add(descriptor);
        }

        var scheduled = false;
        try
        {
            scheduled = _queue.Writer.TryWrite(snapshot);
            return scheduled;
        }
        finally
        {
            if (!scheduled)
            {
                lock (_lock)
                {
                    _pending.Remove(descriptor);
                }
            }
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _queue.Writer.TryComplete();
        }

        _stopping.Cancel();
        _worker.GetAwaiter().GetResult();
        _stopping.Dispose();
        while (_queue.Reader.TryRead(out _)) { }

        while (_completions.TryDequeue(out _))
        {
        }

        lock (_lock)
        {
            _pending.Clear();
        }
    }

    private async Task WorkerLoop()
    {
        await foreach (var snapshot in _queue.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            lock (_lock)
            {
                if (_disposed) { break; }
            }

            var descriptor = new AuthorityChunkDescriptor(snapshot.Coords, snapshot.ContentVersion);
            try
            {
                var encoded = _encode(snapshot, _stopping.Token);
                _stopping.Token.ThrowIfCancellationRequested();
                _completions.Enqueue(new EncodingCompletion(descriptor, encoded, null));
            }
            catch (OperationCanceledException) when (_stopping.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _completions.Enqueue(new EncodingCompletion(descriptor, null, exception));
            }
        }
    }

    private sealed record EncodingCompletion(
        AuthorityChunkDescriptor Descriptor,
        EncodedTerrainChunk? Encoded,
        Exception? Error
    );
}
