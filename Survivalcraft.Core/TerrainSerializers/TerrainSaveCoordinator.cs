using System.Buffers;
using System.Threading.Channels;

namespace Game.TerrainSerializers;

public sealed class TerrainSaveCoordinator : IDisposable
{
    private const int _defaultQueueCapacity = 4;

    // Monitor.Wait/PulseAll require the monitor used by lock(object), not System.Threading.Lock.
    private readonly object _gate = new();

    private readonly Dictionary<Point2, TerrainSaveSnapshot> _latestSnapshots = new();

    private readonly Dictionary<Point2, TerrainUnloadReservation> _unloadReservations = new();

    private readonly int _maximumSnapshots;

    private sealed record WriteBatch(TerrainSaveSnapshot[] Snapshots, TaskCompletionSource? Completion = null);

    private readonly Channel<WriteBatch> _queue;

    private bool _fullSaveActive;

    private readonly Task _worker;

    private readonly Action<Point2, int[], long[]> _writer;

    private bool _accepting = true;

    private bool _disposed;

    private int _outstandingCount;

    private int _bulkOutstandingCount;

    public TerrainSaveCoordinator(
        Action<Point2, int[], long[]> writer,
        int queueCapacity = _defaultQueueCapacity
    )
    {
        ArgumentNullException.ThrowIfNull(writer);
        if (queueCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(queueCapacity));
        }

        _writer = writer;
        _maximumSnapshots = queueCapacity;
        _queue = Channel.CreateBounded<WriteBatch>(
            new BoundedChannelOptions(checked(queueCapacity + 1))
            {
                AllowSynchronousContinuations = false,
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = true
            }
        );
        _worker = Task.Run(WorkerLoop);
    }

    public bool CanAcceptUnloadSnapshot
    {
        get
        {
            lock (_gate)
            {
                return _accepting && RetainedSnapshotCount < _maximumSnapshots;
            }
        }
    }

    // The caller holds _gate. Failed writes still own their recovery buffers after the worker finishes.
    private int RetainedSnapshotCount =>
        _outstandingCount -
        _bulkOutstandingCount +
        _latestSnapshots.Values.Count(snapshot => snapshot.Failed) +
        _unloadReservations.Count -
        _unloadReservations.Keys.Count(coords =>
            _latestSnapshots.TryGetValue(coords, out var snapshot) && snapshot.Failed
        );

    public int OutstandingCount
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
        return chunk is { HasContents: true, ModificationCounter: > 0 };
    }

    /// <summary>Reserves an unload slot before callbacks can dirty the chunk or reenter saving.</summary>
    public TerrainUnloadReservation? TryReserveUnload(TerrainChunk chunk)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        ObjectDisposedException.ThrowIf(chunk.IsDisposed, chunk);
        lock (_gate)
        {
            if (!_accepting || _unloadReservations.ContainsKey(chunk.Coords)) { return null; }

            var replacesFailure = _latestSnapshots.TryGetValue(chunk.Coords, out var previous) && previous.Failed;
            var additionalCapacity = replacesFailure ? 0 : 1;
            if (RetainedSnapshotCount + additionalCapacity > _maximumSnapshots) { return null; }

            var reservation = new TerrainUnloadReservation(this, chunk);
            _unloadReservations.Add(chunk.Coords, reservation);
            return reservation;
        }
    }

    public bool TryQueueChunkForUnload(TerrainChunk chunk)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        ObjectDisposedException.ThrowIf(chunk.IsDisposed, chunk);
        if (!RequiresSave(chunk)) { return true; }

        using var reservation = TryReserveUnload(chunk);
        return reservation?.Commit(chunk) ?? false;
    }

    internal bool CommitUnload(TerrainUnloadReservation reservation, TerrainChunk chunk)
    {
        lock (_gate)
        {
            try
            {
                if (!_accepting ||
                    !_unloadReservations.TryGetValue(reservation.Coords, out var pending) ||
                    !ReferenceEquals(pending, reservation)) { return false; }

                ArgumentNullException.ThrowIfNull(chunk);
                ObjectDisposedException.ThrowIf(chunk.IsDisposed, chunk);
                if (!reservation.Matches(chunk))
                {
                    throw new ArgumentException(
                        "An unload reservation belongs to one chunk allocation.",
                        nameof(chunk)
                    );
                }

                if (!RequiresSave(chunk)) { return true; }

                _latestSnapshots.TryGetValue(chunk.Coords, out var previous);
                var snapshot = TerrainSaveSnapshot.Capture(chunk);
                _latestSnapshots[chunk.Coords] = snapshot;
                if (!_queue.Writer.TryWrite(new WriteBatch([snapshot])))
                {
                    if (previous != null) { _latestSnapshots[chunk.Coords] = previous; }
                    else { _latestSnapshots.Remove(chunk.Coords); }

                    snapshot.Dispose();
                    return false;
                }

                if (previous is { Failed: true }) { previous.Dispose(); }

                _outstandingCount++;
                chunk.ModificationCounter = 0;
                return true;
            }
            finally { ReleaseUnloadUnderGate(reservation); }
        }
    }

    internal void ReleaseUnload(TerrainUnloadReservation reservation)
    {
        lock (_gate) { ReleaseUnloadUnderGate(reservation); }
    }

    private void ReleaseUnloadUnderGate(TerrainUnloadReservation reservation)
    {
        if (_unloadReservations.TryGetValue(reservation.Coords, out var pending) &&
            ReferenceEquals(pending, reservation))
        {
            _unloadReservations.Remove(reservation.Coords);
            Monitor.PulseAll(_gate);
        }
    }

    /// <summary>Captures one save point under the caller's terrain lease; compression and writes remain on the ordered writer.</summary>
    public Task CaptureAndQueueSave(IEnumerable<TerrainChunk> chunks)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(!_accepting, this);
            if (_fullSaveActive) { throw new InvalidOperationException("A full terrain save is already pending."); }

            var captured = new List<(TerrainChunk Chunk, TerrainSaveSnapshot Snapshot)>();
            var transferred = false;
            try
            {
                foreach (var chunk in chunks)
                {
                    if (RequiresSave(chunk)) { captured.Add((chunk, TerrainSaveSnapshot.Capture(chunk))); }
                }

                var replaced = captured.Select(item => item.Chunk.Coords).ToHashSet();
                var retries = _latestSnapshots.Values
                    .Where(snapshot => snapshot.Failed && !replaced.Contains(snapshot.Coords)).ToArray();
                var snapshots = retries.Concat(captured.Select(item => item.Snapshot)).ToArray();
                var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                // Ordinary unloads retain at most _maximumSnapshots slots. One additional queue
                // slot is reserved for this single bulk save, regardless of its resident input size.
                if (!_queue.Writer.TryWrite(new WriteBatch(snapshots, completion)))
                {
                    throw new InvalidOperationException("The reserved terrain save slot is unavailable.");
                }

                transferred = true;
                _fullSaveActive = true;
                _outstandingCount += snapshots.Length;
                _bulkOutstandingCount += snapshots.Length;
                foreach (var retry in retries)
                {
                    retry.Failed = false;
                    retry.Error = null;
                }

                foreach (var (chunk, snapshot) in captured)
                {
                    if (_latestSnapshots.TryGetValue(chunk.Coords, out var previous) && previous.Failed)
                    {
                        previous.Dispose();
                    }

                    _latestSnapshots[chunk.Coords] = snapshot;
                    chunk.ModificationCounter = 0;
                }

                return completion.Task;
            }
            catch
            {
                if (!transferred)
                {
                    foreach (var item in captured) { item.Snapshot.Dispose(); }
                }

                throw;
            }
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
            foreach (var snapshot in _latestSnapshots.Values)
            {
                // A failed baseline shares its slot with a replacement reservation. Retrying
                // it needs an extra slot until the old writer finishes; never steal the lease.
                var reservedReplacement = _unloadReservations.ContainsKey(snapshot.Coords);
                if (snapshot.Failed &&
                    (!reservedReplacement || RetainedSnapshotCount < _maximumSnapshots) &&
                    _queue.Writer.TryWrite(new WriteBatch([snapshot])))
                {
                    snapshot.Failed = false;
                    snapshot.Error = null;
                    _outstandingCount++;
                }
            }

            while (_outstandingCount > 0 || _fullSaveActive)
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
        lock (_gate)
        {
            if (_disposed || !_accepting) { return; }

            _accepting = false;
            foreach (var reservation in _unloadReservations.Values) { reservation.Revoke(this); }

            _unloadReservations.Clear();
        }

        Exception? flushError = null;
        try
        {
            Flush();
        }
        catch (Exception e)
        {
            flushError = e;
        }

        _queue.Writer.TryComplete();

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
        await foreach (var batch in _queue.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            var failures = new List<Exception>();
            foreach (var snapshot in batch.Snapshots)
            {
                if (await WriteSnapshot(snapshot, batch.Completion != null).ConfigureAwait(false) is { } error)
                {
                    failures.Add(error);
                }
            }

            if (batch.Completion != null)
            {
                lock (_gate)
                {
                    failures.AddRange(
                        _latestSnapshots.Values.Where(snapshot => snapshot.Failed).Select(snapshot => snapshot.Error!)
                    );
                    _fullSaveActive = false;
                    Monitor.PulseAll(_gate);
                }

                if (failures.Count == 0) { batch.Completion.TrySetResult(); }
                else
                {
                    batch.Completion.TrySetException(
                        new AggregateException("Terrain save failed.", failures.Distinct())
                    );
                }
            }
        }
    }

    private async Task<Exception?> WriteSnapshot(TerrainSaveSnapshot snapshot, bool bulk)
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
            if (bulk) { _bulkOutstandingCount--; }

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
                    Log.Error(
                        $"Error saving terrain snapshot ({snapshot.Coords.X},{snapshot.Coords.Y}).{Environment.NewLine}{error}"
                    );
                }
            }
            else
            {
                snapshot.Dispose();
            }

            Monitor.PulseAll(_gate);
        }

        return error;
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
