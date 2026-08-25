using System.Buffers;
using System.Threading.Channels;

namespace Game.TerrainSerializers;

internal sealed class TerrainSaveCoordinator : IDisposable
{
    private const int _defaultQueueCapacity = 4;

    private readonly object _gate = new();

    private readonly Dictionary<Point2, TerrainSaveSnapshot> _latestSnapshots = new();

    private readonly int _maximumOutstanding;

    private readonly Channel<TerrainSaveSnapshot> _queue;

    private readonly Task _worker;

    private readonly Action<Point2, int[], long[]> _writer;

    private bool _accepting = true;

    private bool _disposed;

    private int _outstandingCount;

    public TerrainSaveCoordinator(Action<Point2, int[], long[]> writer,
        int queueCapacity = _defaultQueueCapacity)
    {
        ArgumentNullException.ThrowIfNull(writer);
        if (queueCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(queueCapacity));
        }

        _writer = writer;
        _maximumOutstanding = queueCapacity;
        _queue = Channel.CreateBounded<TerrainSaveSnapshot>(new BoundedChannelOptions(queueCapacity)
        {
            AllowSynchronousContinuations = false,
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = true
        });
        _worker = Task.Run(WorkerLoop);
    }

    public bool CanAcceptUnloadSnapshot
    {
        get
        {
            lock (_gate)
            {
                return _accepting && _outstandingCount < _maximumOutstanding;
            }
        }
    }

    internal int OutstandingCount
    {
        get
        {
            lock (_gate)
            {
                return _outstandingCount;
            }
        }
    }

    public static bool RequiresSave(TerrainChunk chunk)
    {
        return chunk is { MainThreadState: > TerrainChunkState.InvalidContents4, ModificationCounter: > 0 };
    }

    public bool TryQueueChunkForUnload(TerrainChunk chunk)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        if (!RequiresSave(chunk))
        {
            return true;
        }

        lock (_gate)
        {
            if (!_accepting || _outstandingCount >= _maximumOutstanding)
            {
                return false;
            }

            var snapshot = TerrainSaveSnapshot.Capture(chunk);
            _latestSnapshots.TryGetValue(chunk.Coords, out var previous);
            _latestSnapshots[chunk.Coords] = snapshot;
            if (!_queue.Writer.TryWrite(snapshot))
            {
                if (previous != null)
                {
                    _latestSnapshots[chunk.Coords] = previous;
                }
                else
                {
                    _latestSnapshots.Remove(chunk.Coords);
                }

                snapshot.Dispose();
                return false;
            }

            if (previous is { Failed: true })
            {
                previous.Dispose();
            }

            _outstandingCount++;
            chunk.ModificationCounter = 0;
            return true;
        }
    }

    public bool TryRestorePendingSnapshot(TerrainChunk chunk)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        lock (_gate)
        {
            if (!_latestSnapshots.TryGetValue(chunk.Coords, out var snapshot))
            {
                return false;
            }

            snapshot.CopyTo(chunk);
            _latestSnapshots.Remove(chunk.Coords);
            if (snapshot.Failed)
            {
                snapshot.Dispose();
            }

            // The snapshot is no longer the sole owner of the newest state. Mark the
            // reloaded chunk dirty so a later unload/save cannot depend on an in-flight write.
            chunk.ModificationCounter = 1;
            return true;
        }
    }

    public void Flush()
    {
        lock (_gate)
        {
            while (_outstandingCount > 0)
            {
                Monitor.Wait(_gate);
            }

            var failures = _latestSnapshots.Values
                .Where(snapshot => snapshot.Failed)
                .Select(snapshot => snapshot.Error!)
                .ToArray();
            if (failures.Length > 0)
            {
                throw new AggregateException("One or more terrain chunks could not be saved.", failures);
            }
        }
    }

    public void Dispose()
    {
        Exception? flushError = null;
        try
        {
            Flush();
        }
        catch (Exception e)
        {
            flushError = e;
        }

        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _accepting = false;
            _queue.Writer.TryComplete();
        }

        _worker.GetAwaiter().GetResult();
        lock (_gate)
        {
            foreach (var snapshot in _latestSnapshots.Values.Distinct())
            {
                snapshot.Dispose();
            }

            _latestSnapshots.Clear();
            _disposed = true;
        }

        if (flushError != null)
        {
            throw flushError;
        }
    }

    private async Task WorkerLoop()
    {
        await foreach (var snapshot in _queue.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            Exception? error = null;
            for (var attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    _writer(snapshot.Coords, snapshot.Cells, snapshot.Shafts);
                    error = null;
                    break;
                }
                catch (Exception e)
                {
                    error = e;
                    if (attempt < 3)
                    {
                        await Task.Delay(attempt * 25).ConfigureAwait(false);
                    }
                }
            }

            lock (_gate)
            {
                _outstandingCount--;
                if (_latestSnapshots.TryGetValue(snapshot.Coords, out var latest) &&
                    ReferenceEquals(latest, snapshot))
                {
                    if (error == null)
                    {
                        _latestSnapshots.Remove(snapshot.Coords);
                        snapshot.Dispose();
                    }
                    else
                    {
                        snapshot.Failed = true;
                        snapshot.Error = error;
                        Log.Error(ExceptionManager.MakeFullErrorMessage(
                            $"Error saving terrain snapshot ({snapshot.Coords.X},{snapshot.Coords.Y}).", error));
                    }
                }
                else
                {
                    snapshot.Dispose();
                }

                Monitor.PulseAll(_gate);
            }
        }
    }
}

internal sealed class TerrainSaveSnapshot : IDisposable
{
    private const int _cellCount = 16 * 16 * 256;

    private const int _shaftCount = 16 * 16;

    private int[]? _cells;

    private long[]? _shafts;

    private TerrainSaveSnapshot(Point2 coords, int[] cells, long[] shafts)
    {
        Coords = coords;
        _cells = cells;
        _shafts = shafts;
    }

    public int[] Cells => _cells ?? throw new ObjectDisposedException(nameof(TerrainSaveSnapshot));

    public Point2 Coords { get; }

    public Exception? Error { get; set; }

    public bool Failed { get; set; }

    public long[] Shafts => _shafts ?? throw new ObjectDisposedException(nameof(TerrainSaveSnapshot));

    public static TerrainSaveSnapshot Capture(TerrainChunk chunk)
    {
        var cells = ArrayPool<int>.Shared.Rent(_cellCount);
        var shafts = ArrayPool<long>.Shared.Rent(_shaftCount);
        try
        {
            Array.Copy(chunk.Cells, cells, _cellCount);
            Array.Copy(chunk.Shafts, shafts, _shaftCount);
            return new TerrainSaveSnapshot(chunk.Coords, cells, shafts);
        }
        catch
        {
            ArrayPool<int>.Shared.Return(cells);
            ArrayPool<long>.Shared.Return(shafts);
            throw;
        }
    }

    public void CopyTo(TerrainChunk chunk)
    {
        Array.Copy(Cells, chunk.Cells, _cellCount);
        Array.Copy(Shafts, chunk.Shafts, _shaftCount);
    }

    public void Dispose()
    {
        var cells = Interlocked.Exchange(ref _cells, null);
        if (cells != null)
        {
            ArrayPool<int>.Shared.Return(cells);
        }

        var shafts = Interlocked.Exchange(ref _shafts, null);
        if (shafts != null)
        {
            ArrayPool<long>.Shared.Return(shafts);
        }
    }
}
