namespace Game.Terrains.Execution;

/// <summary>Thread-affine, reentrant access to a world's mutable terrain.</summary>
/// <remarks>Closing cancels waiters; an existing owner can still unwind and release its lease.</remarks>
public sealed class TerrainAccessGate : IDisposable
{
    private readonly object _sync = new();

    private readonly SemaphoreSlim _available = new(1, 1);

    private readonly CancellationTokenSource _closing = new();

    private int _owner;

    private int _depth;

    private int _waiters;

    private bool _closed;

    private bool _resourcesDisposed;

    internal bool IsHeldOnceByCurrentThread
    {
        get
        {
            lock (_sync) { return _owner == Environment.CurrentManagedThreadId && _depth == 1; }
        }
    }

    public bool IsHeldByCurrentThread
    {
        get
        {
            lock (_sync) { return _owner == Environment.CurrentManagedThreadId; }
        }
    }

    public void Enter()
    {
        if (!TryEnter(Timeout.Infinite)) { throw new ObjectDisposedException(nameof(TerrainAccessGate)); }
    }

    public bool TryEnter(int millisecondsTimeout = 0)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(millisecondsTimeout, Timeout.Infinite);
        var current = Environment.CurrentManagedThreadId;
        CancellationToken cancellation;
        lock (_sync)
        {
            if (_owner == current)
            {
                _depth++;
                return true;
            }

            if (_closed) { return false; }

            _waiters++;
            cancellation = _closing.Token;
        }

        var acquired = false;
        var accepted = false;
        try { acquired = _available.Wait(millisecondsTimeout, cancellation); }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        finally
        {
            lock (_sync)
            {
                _waiters--;
                if (acquired && !_closed)
                {
                    _owner = current;
                    _depth = 1;
                    accepted = true;
                }
                else if (acquired) { _available.Release(); }

                DisposeResourcesIfUnused();
            }
        }

        return accepted;
    }

    public void Exit()
    {
        lock (_sync)
        {
            if (_owner != Environment.CurrentManagedThreadId)
            {
                throw new SynchronizationLockException("Only the terrain access owner can release its lease.");
            }

            if (--_depth != 0) { return; }

            _owner = 0;
            _available.Release();
            DisposeResourcesIfUnused();
        }
    }

    public void Close()
    {
        lock (_sync)
        {
            if (_closed) { return; }

            _closed = true;
            _closing.Cancel();
            DisposeResourcesIfUnused();
        }
    }

    public void Dispose() => Close();

    private void DisposeResourcesIfUnused()
    {
        if (!_closed || _resourcesDisposed || _depth != 0 || _waiters != 0) { return; }

        _resourcesDisposed = true;
        _available.Dispose();
        _closing.Dispose();
    }
}
