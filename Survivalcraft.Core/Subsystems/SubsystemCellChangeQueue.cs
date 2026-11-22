using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Terrains.Execution;

namespace Game.Subsystems;

/// <summary>在权威端更新线程上分批执行延后的自然变化，每批有数量上限。</summary>
public class SubsystemCellChangeQueue : Subsystem, IUpdateable
{
    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Authority;

    public int PendingChangesCount => _changes?.Count ?? 0;

    private DeferredTerrainChanges? _changes;

    private TerrainMainWorkRequest? _request;

    private SubsystemTerrain _subsystemTerrain = null!;

    private SubsystemTime _subsystemTime = null!;

    private bool _disposed;

    private bool _updating;

    UpdateOrder IUpdateable.UpdateOrder => UpdateOrder.Default;

    void IUpdateable.Update(float dt)
    {
        if (_disposed || _changes == null || !WorldRuntimeContext.IsProjectActive(Project)) { return; }

        if (_subsystemTime.PeriodicGameTimeEvent(20.0, 0.0) || _changes.IsFull) { _changes.StartBatch(); }

        if (!_changes.HasActiveBatch) { return; }

        if (_updating) { return; }

        _updating = true;
        try
        {
            var request = _request ??= _subsystemTerrain.WorkExecutor.CreateMainRequest();
            if (!request.TryEnter()) { return; }

            // Pause the worker for this short batch; callbacks run without an outer terrain access lease.
            request.YieldAccess();
            try { _changes.Drain(32, TimeSpan.FromMilliseconds(2)); }
            finally
            {
                request.Dispose();
                if (ReferenceEquals(_request, request)) { _request = null; }
            }
        }
        finally { _updating = false; }
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _changes = new DeferredTerrainChanges(
            ReadCell,
            (point, value) => _subsystemTerrain.ChangeCell(point.X, point.Y, point.Z, value)
        );
    }

    /// <summary>返回 false 时保留自然状态，可在下一次常规轮询时重试。</summary>
    public bool TryQueueCellChange(int x, int y, int z, int value, bool applyImmediately = false)
    {
        if (_disposed ||
            !WorldRuntimeContext.IsProjectActive(Project) ||
            !WorldRuntimeContext.For(Project).IsAuthority) { return false; }

        if (applyImmediately)
        {
            _subsystemTerrain.ChangeCell(x, y, z, value);
            return true;
        }

        return _changes!.TryEnqueue(new Point3(x, y, z), value);
    }

    /// <summary>仅在整组变更都能入队时接纳请求，应用时仍逐格检查条件。</summary>
    public bool TryQueueCellChanges(ReadOnlySpan<(Point3 Point, int Value)> changes)
    {
        return !_disposed &&
               WorldRuntimeContext.IsProjectActive(Project) &&
               WorldRuntimeContext.For(Project).IsAuthority &&
               _changes!.TryEnqueue(changes);
    }

    public override void Dispose()
    {
        _disposed = true;
        _request?.Dispose();
        _request = null;
        // These are uncommitted natural intents, as before; later scans regenerate them after reload.
        _changes?.Clear();
    }

    private DeferredTerrainChanges.Cell? ReadCell(Point3 point)
    {
        var chunk = _subsystemTerrain.Terrain.GetChunkAtCell(point.X, point.Z, false);
        return chunk is { HasContents: true }
            ? new DeferredTerrainChanges.Cell(
                _subsystemTerrain.Terrain.GetCellValue(point.X, point.Y, point.Z),
                chunk.AllocationGeneration
            )
            : null;
    }
}
