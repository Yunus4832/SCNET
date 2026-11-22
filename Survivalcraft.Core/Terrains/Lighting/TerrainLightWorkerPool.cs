using System.Diagnostics.CodeAnalysis;

namespace Game.Terrains.Lighting;

/// <summary>有界接纳使用不可变输入的任务，并向管理者返回终态消息。</summary>
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

    /// <summary>有空闲工作槽时启动任务，槽位已满或已经开始关闭时返回 false。</summary>
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

    private Task? _stopping;

    /// <summary>停止接纳任务，请求取消活动任务并返回等待工作线程结束的任务。重复调用返回同一个任务。</summary>
    public Task StopAsync()
    {
        TerrainLightTask[] tasks;
        Task completion;
        lock (_sync)
        {
            if (_stopping != null) { return _stopping; }

            _disposed = true;
            tasks = _active.ToArray();
            _stopping = completion = Task.WhenAll(tasks.Select(task => task.Worker));
        }

        foreach (var task in tasks) { task.Cancel(); }

        return completion;
    }

    public void Dispose() => StopAsync().GetAwaiter().GetResult();
}
