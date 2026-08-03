using System.Collections.Concurrent;
using System.Threading.Channels;

using Game.Network.Serialization;

namespace Game.Network;

internal sealed class NetworkChunkEncoder : IDisposable
{
    private readonly ConcurrentQueue<EncodingCompletion> _completions = new();

    private readonly Func<NetworkChunkSnapshot, EncodedTerrainChunk> _encode;

    private readonly Lock _lock = new();

    private readonly HashSet<TerrainChunk> _pending = new(ReferenceEqualityComparer.Instance);

    private readonly Channel<TerrainChunk> _queue;

    private readonly int _maximumOutstanding;

    private readonly Task _worker;

    private bool _disposed;

    public NetworkChunkEncoder(
        int maximumOutstanding = NetworkTerrainPolicy.DefaultServerChunkCountSendPer,
        Func<NetworkChunkSnapshot, EncodedTerrainChunk>? encode = null)
    {
        if (maximumOutstanding <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumOutstanding));
        }

        _maximumOutstanding = maximumOutstanding;
        _encode = encode ?? NetworkChunkCodec.Encode;
        _queue = Channel.CreateBounded<TerrainChunk>(new BoundedChannelOptions(maximumOutstanding)
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

        var scheduled = false;
        try
        {
            scheduled = _queue.Writer.TryWrite(chunk);
            if (scheduled)
            {
                return true;
            }
        }
        finally
        {
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
        await foreach (var chunk in _queue.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            NetworkChunkSnapshot? snapshot = null;
            try
            {
                snapshot = NetworkChunkSnapshot.TryCapture(chunk);
                _completions.Enqueue(new EncodingCompletion(
                    chunk,
                    snapshot?.Revision ?? chunk.NetworkContentRevision,
                    snapshot == null ? null : _encode(snapshot),
                    null));
            }
            catch (Exception exception)
            {
                _completions.Enqueue(new EncodingCompletion(
                    chunk,
                    snapshot?.Revision ?? chunk.NetworkContentRevision,
                    null,
                    exception));
            }
            finally
            {
                snapshot?.Dispose();
            }
        }
    }

    private sealed record EncodingCompletion(
        TerrainChunk Source,
        long Revision,
        EncodedTerrainChunk? Encoded,
        Exception? Error);
}
