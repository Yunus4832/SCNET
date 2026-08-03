using System.Collections.Concurrent;
using System.Threading.Channels;

using Game.Network.Serialization;

namespace Game.Network;

internal sealed class NetworkChunkEncoder : IDisposable
{
    private const int _defaultQueueCapacity = 4;

    private readonly ConcurrentQueue<EncodingCompletion> _completions = new();

    private readonly Func<NetworkChunkSnapshot, EncodedTerrainChunk> _encode;

    private readonly Lock _lock = new();

    private readonly HashSet<TerrainChunk> _pending = new(ReferenceEqualityComparer.Instance);

    private readonly Channel<NetworkChunkSnapshot> _queue;

    private readonly int _maximumOutstanding;

    private readonly Task _worker;

    private bool _disposed;

    public NetworkChunkEncoder(int queueCapacity = _defaultQueueCapacity,
        Func<NetworkChunkSnapshot, EncodedTerrainChunk>? encode = null)
    {
        if (queueCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(queueCapacity));
        }

        _maximumOutstanding = queueCapacity + 1;
        _encode = encode ?? NetworkChunkCodec.Encode;
        _queue = Channel.CreateBounded<NetworkChunkSnapshot>(new BoundedChannelOptions(queueCapacity)
        {
            AllowSynchronousContinuations = false,
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = true
        });
        _worker = Task.Run(WorkerLoop);
    }

    internal int OutstandingCount
    {
        get
        {
            lock (_lock)
            {
                return _pending.Count;
            }
        }
    }

    public IReadOnlyList<TerrainChunk> DrainCompleted(Terrain terrain, NetworkChunkCache cache)
    {
        ArgumentNullException.ThrowIfNull(terrain);
        ArgumentNullException.ThrowIfNull(cache);

        List<TerrainChunk>? ready = null;
        while (_completions.TryDequeue(out var completion))
        {
            lock (_lock)
            {
                _pending.Remove(completion.Source);
            }

            if (completion.Error != null)
            {
                Log.Warning(
                    $"Failed to encode terrain chunk {completion.Source.Coords}: " +
                    $"{completion.Error.GetType().Name}: {completion.Error.Message}");
                continue;
            }

            var current = terrain.GetChunkAtCoords(completion.Source.Coords.X, completion.Source.Coords.Y);
            if (!ReferenceEquals(current, completion.Source) ||
                completion.Source.NetworkContentRevision != completion.Revision ||
                completion.Encoded == null)
            {
                continue;
            }

            cache.Store(completion.Source, completion.Revision, completion.Encoded);
            (ready ??= []).Add(completion.Source);
        }

        return ready is null ? Array.Empty<TerrainChunk>() : ready;
    }

    public bool TrySchedule(TerrainChunk chunk)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_pending.Contains(chunk))
            {
                return true;
            }

            if (_pending.Count >= _maximumOutstanding)
            {
                return false;
            }

            _pending.Add(chunk);
        }

        NetworkChunkSnapshot? snapshot = null;
        var scheduled = false;
        try
        {
            snapshot = NetworkChunkSnapshot.TryCapture(chunk);
            scheduled = snapshot != null && _queue.Writer.TryWrite(snapshot);
            if (scheduled)
            {
                snapshot = null;
                return true;
            }
        }
        finally
        {
            snapshot?.Dispose();
            if (!scheduled)
            {
                lock (_lock)
                {
                    _pending.Remove(chunk);
                }
            }
        }

        return false;
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

        _worker.GetAwaiter().GetResult();
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
            try
            {
                _completions.Enqueue(new EncodingCompletion(
                    snapshot.Source,
                    snapshot.Revision,
                    _encode(snapshot),
                    null));
            }
            catch (Exception exception)
            {
                _completions.Enqueue(new EncodingCompletion(
                    snapshot.Source,
                    snapshot.Revision,
                    null,
                    exception));
            }
            finally
            {
                snapshot.Dispose();
            }
        }
    }

    private sealed record EncodingCompletion(
        TerrainChunk Source,
        long Revision,
        EncodedTerrainChunk? Encoded,
        Exception? Error);
}
