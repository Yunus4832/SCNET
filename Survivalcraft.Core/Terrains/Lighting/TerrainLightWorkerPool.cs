using System.Diagnostics.CodeAnalysis;

namespace Game.Terrains.Lighting;

/// <summary>Admits a bounded number of immutable jobs and returns terminal owner messages.</summary>
internal sealed class TerrainLightWorkerPool : IDisposable
{
    private readonly int _workerCount;

    private readonly Func<TerrainLightJob, CancellationToken, TerrainLightSnapshot> _compute;

    private readonly Action<TerrainLightCompletion> _completed;

    private readonly HashSet<TerrainLightTask> _active = [];

    private int _computing;

    private readonly object _sync = new();

    private bool _disposed;

    public TerrainLightWorkerPool(
        int workerCount,
        Func<TerrainLightJob, CancellationToken, TerrainLightSnapshot> compute,
        Action<TerrainLightCompletion> completed
    )
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(workerCount, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(workerCount, 2);
        ArgumentNullException.ThrowIfNull(compute);
        ArgumentNullException.ThrowIfNull(completed);
        _workerCount = workerCount;
        _compute = compute;
        _completed = completed;
    }

    public bool HasCapacity
    {
        get
        {
            lock (_sync) { return !_disposed && _computing < _workerCount; }
        }
    }

    public bool TrySchedule(TerrainLightJob job, [NotNullWhen(true)] out TerrainLightTask? task)
    {
        ArgumentNullException.ThrowIfNull(job);
        lock (_sync)
        {
            task = null;
            if (_disposed || _computing >= _workerCount) { return false; }

            var admitted = new TerrainLightTask(job);
            _active.Add(admitted);
            _computing++;
            try { admitted.Start(_compute, Completed); }
            catch
            {
                _computing--;
                _active.Remove(admitted);
                admitted.DisposeCancellation();
                throw;
            }

            _ = admitted.Worker.ContinueWith(
                _ =>
                {
                    lock (_sync) { _active.Remove(admitted); }
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default
            );
            task = admitted;
            return true;
        }
    }

    private void Completed(TerrainLightCompletion completion)
    {
        lock (_sync) { _computing--; }

        _completed(completion);
    }

    public void Dispose()
    {
        TerrainLightTask[] tasks;
        lock (_sync)
        {
            if (_disposed) { return; }

            _disposed = true;
            tasks = _active.ToArray();
        }

        foreach (var task in tasks) { task.Cancel(); }

        // Closing joins only private computations and callbacks, which never require world access.
        Task.WhenAll(tasks.Select(task => task.Worker)).GetAwaiter().GetResult();
    }
}
