namespace Game.Terrains.Lighting;

/// <summary>Cancellation and lifetime of one admitted light calculation.</summary>
internal sealed class TerrainLightTask(TerrainLightJob job)
{
    private readonly CancellationTokenSource _cancellation = new();

    private readonly object _sync = new();

    private bool _disposed;

    private volatile bool _cancelRequested;

    public bool CancellationRequested => _cancelRequested;

    public TerrainLightJob Job { get; } = job;

    public Task Worker { get; private set; } = null!;

    internal void Start(
        Func<TerrainLightJob, CancellationToken, TerrainLightSnapshot> compute,
        Action<TerrainLightCompletion> completed
    )
    {
        Worker = Task.Run(() =>
            {
                var started = Stopwatch.GetTimestamp();
                TerrainLightSnapshot? snapshot = null;
                Exception? error = null;
                try { snapshot = compute(Job, _cancellation.Token); }
                catch (OperationCanceledException) when (_cancellation.IsCancellationRequested) { }
                catch (Exception failure) { error = failure; }

                if (_cancellation.IsCancellationRequested) { snapshot = null; }

                try
                {
                    completed(new TerrainLightCompletion(this, snapshot, error, Stopwatch.GetElapsedTime(started)));
                }
                finally { DisposeCancellation(); }
            }
        );
    }

    public void Cancel()
    {
        lock (_sync)
        {
            _cancelRequested = true;
            if (!_disposed) { _cancellation.Cancel(); }
        }
    }

    internal void DisposeCancellation()
    {
        lock (_sync)
        {
            if (_disposed) { return; }

            _disposed = true;
            _cancellation.Dispose();
        }
    }
}
