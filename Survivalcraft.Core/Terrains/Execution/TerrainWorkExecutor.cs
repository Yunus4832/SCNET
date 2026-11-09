namespace Game.Terrains.Execution;

/// <summary>One world's serial terrain worker, wakeups and durable foreground handoffs.</summary>
public sealed class TerrainWorkExecutor(Func<bool> step, TerrainAccessGate? access = null) : IDisposable
{
    private readonly object _signal = new();

    private readonly ManualResetEventSlim _wake = new(false);

    private readonly Func<bool> _step = step ?? throw new ArgumentNullException(nameof(step));

    private readonly HashSet<TerrainMainWorkRequest> _requests = [];

    private Task? _worker;

    private CancellationToken _lifetime;

    private int _workerThread;

    private int _transientRequests;

    private int _transientOwner;

    private int _transientDepth;

    private long _wakeRevision;

    private bool _disposed;

    private bool _workerStopped;

    private bool _signalDisposed;


    public TerrainAccessGate Access { get; } = access ?? new TerrainAccessGate();

    private bool HasRequests => _transientRequests != 0 || _requests.Count != 0;

    public bool HasMainRequest
    {
        get
        {
            lock (_signal) { return !_disposed && HasRequests; }
        }
    }

    public TerrainMainWorkRequest CreateMainRequest()
    {
        lock (_signal)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return new TerrainMainWorkRequest(this);
        }
    }

    public void Start(CancellationToken lifetime = default)
    {
        lock (_signal)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_worker != null) { throw new InvalidOperationException("Terrain worker is already started."); }

            _lifetime = lifetime;
            _worker = Task.Factory.StartNew(
                Run,
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default
            );
        }
    }

    public void Wake()
    {
        lock (_signal)
        {
            if (_disposed) { return; }

            _wakeRevision++;
            if (!HasRequests) { _wake.Set(); }
        }
    }

    public bool TryEnterMain(int millisecondsTimeout = 0)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(millisecondsTimeout, Timeout.Infinite);
        lock (_signal)
        {
            if (_disposed) { return false; }

            _transientRequests++;
            _wake.Reset();
        }

        var acquired = false;
        try
        {
            acquired = Access.TryEnter(millisecondsTimeout);
            if (acquired)
            {
                lock (_signal)
                {
                    if (_disposed)
                    {
                        Access.Exit();
                        acquired = false;
                    }
                    else
                    {
                        _transientOwner = Environment.CurrentManagedThreadId;
                        _transientDepth++;
                    }
                }
            }
        }
        finally
        {
            if (!acquired)
            {
                lock (_signal)
                {
                    _transientRequests--;
                    ResumeIfUnblocked();
                }
            }
        }

        return acquired;
    }

    public void EnterMain()
    {
        if (!TryEnterMain(Timeout.Infinite)) { throw new ObjectDisposedException(nameof(TerrainWorkExecutor)); }
    }

    public void LeaveMain()
    {
        lock (_signal)
        {
            if (_transientDepth == 0 || _transientOwner != Environment.CurrentManagedThreadId)
            {
                throw new SynchronizationLockException("No transient terrain access is owned by this thread.");
            }

            Access.Exit();
            if (--_transientDepth == 0) { _transientOwner = 0; }

            _transientRequests--;
            ResumeIfUnblocked();
        }
    }

    internal bool HasAccess(TerrainMainWorkRequest request)
    {
        lock (_signal)
        {
            return request.depth > 0 && request.ownerThread == Environment.CurrentManagedThreadId;
        }
    }

    internal bool IsPending(TerrainMainWorkRequest request)
    {
        lock (_signal) { return request.pending; }
    }

    internal bool TryEnter(TerrainMainWorkRequest request, int millisecondsTimeout)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(millisecondsTimeout, Timeout.Infinite);
        lock (_signal)
        {
            if (_disposed || request.disposed) { return false; }

            if (request.depth > 0 && request.ownerThread != Environment.CurrentManagedThreadId)
            {
                throw new SynchronizationLockException("A foreground request already has another owner.");
            }

            request.pending = true;
            _requests.Add(request);
            _wake.Reset();
        }

        var acquired = Access.TryEnter(millisecondsTimeout);
        if (!acquired) { return false; }

        lock (_signal)
        {
            if (_disposed || request.disposed)
            {
                Access.Exit();
                RemovePending(request);
                ResumeIfUnblocked();
                return false;
            }

            request.depth++;
            request.ownerThread = Environment.CurrentManagedThreadId;
            return true;
        }
    }

    internal void Leave(TerrainMainWorkRequest request)
    {
        lock (_signal)
        {
            if (request.depth == 0 || request.ownerThread != Environment.CurrentManagedThreadId)
            {
                throw new SynchronizationLockException("Only the foreground request owner can release it.");
            }

            Access.Exit();
            if (--request.depth == 0)
            {
                request.ownerThread = 0;
                RemovePending(request);
            }

            ResumeIfUnblocked();
        }
    }

    internal void YieldAccess(TerrainMainWorkRequest request)
    {
        lock (_signal)
        {
            if (request.depth != 1 || request.ownerThread != Environment.CurrentManagedThreadId)
            {
                throw new SynchronizationLockException("Yielding access requires a single owned foreground lease.");
            }

            Access.Exit();
            request.depth = 0;
            request.ownerThread = 0;
            if (_disposed || request.disposed) { RemovePending(request); }

            ResumeIfUnblocked();
        }
    }

    internal void Cancel(TerrainMainWorkRequest request)
    {
        lock (_signal)
        {
            request.disposed = true;
            if (request.depth == 0) { RemovePending(request); }

            ResumeIfUnblocked();
        }
    }

    public void Dispose()
    {
        Task? worker;
        lock (_signal)
        {
            if (_disposed) { return; }

            _disposed = true;
            foreach (var request in _requests) { request.pending = false; }

            _requests.Clear();
            worker = _worker;
            _wake.Set();
        }

        Access.Close();
        if (worker != null && Volatile.Read(ref _workerThread) != Environment.CurrentManagedThreadId)
        {
            worker.GetAwaiter().GetResult();
        }

        lock (_signal)
        {
            if (worker == null) { _workerStopped = true; }

            DisposeSignalIfStopped();
        }
    }

    private void RemovePending(TerrainMainWorkRequest request)
    {
        request.pending = false;
        _requests.Remove(request);
    }

    private void ResumeIfUnblocked()
    {
        if (_disposed || HasRequests) { return; }

        _wakeRevision++;
        _wake.Set();
    }

    private void Run()
    {
        Volatile.Write(ref _workerThread, Environment.CurrentManagedThreadId);
        try
        {
            while (true)
            {
                _wake.Wait();
                lock (_signal)
                {
                    if (_disposed || _lifetime.IsCancellationRequested) { return; }
                }

                if (!Access.TryEnter(Timeout.Infinite)) { return; }

                try
                {
                    long revision;
                    lock (_signal)
                    {
                        if (_disposed) { return; }

                        if (HasRequests)
                        {
                            _wake.Reset();
                            continue;
                        }

                        revision = _wakeRevision;
                    }

                    if (_step())
                    {
                        lock (_signal)
                        {
                            if (!_disposed && revision == _wakeRevision) { _wake.Reset(); }
                        }
                    }
                }
                catch (Exception exception) when (IsExpectedCancellation(exception)) { return; }
                catch (Exception exception) { Log.Error($"Terrain update worker failed. {exception}"); }
                finally { Access.Exit(); }
            }
        }
        finally
        {
            lock (_signal)
            {
                _workerStopped = true;
                DisposeSignalIfStopped();
            }
        }
    }

    private bool IsExpectedCancellation(Exception exception)
    {
        if (!_lifetime.IsCancellationRequested) { return false; }

        if (exception is OperationCanceledException) { return true; }

        if (exception is not AggregateException aggregate) { return false; }

        var failures = aggregate.Flatten().InnerExceptions;
        return failures.Count > 0 && failures.All(failure => failure is OperationCanceledException);
    }

    private void DisposeSignalIfStopped()
    {
        if (!_disposed || !_workerStopped || _signalDisposed) { return; }

        _signalDisposed = true;
        _wake.Dispose();
    }
}
