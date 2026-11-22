namespace Game.Terrains.Execution;

/// <summary>持续有效的前台请求，其他前台操作不能消耗它。</summary>
public sealed class TerrainMainWorkRequest : IDisposable
{
    internal readonly TerrainWorkExecutor executor;

    internal bool pending;

    internal bool disposed;

    internal int depth;

    internal int ownerThread;

    internal TerrainMainWorkRequest(TerrainWorkExecutor executor)
    {
        this.executor = executor;
    }

    public bool HasAccess => executor.HasAccess(this);

    public bool IsPending => executor.IsPending(this);

    public bool TryEnter(int millisecondsTimeout = 0) => executor.TryEnter(this, millisecondsTimeout);

    public void Leave() => executor.Leave(this);

    /// <summary>释放该请求唯一的访问租约，同时保留暂停工作线程的请求。</summary>
    public void YieldAccess() => executor.YieldAccess(this);

    public void Dispose() => executor.Cancel(this);
}
