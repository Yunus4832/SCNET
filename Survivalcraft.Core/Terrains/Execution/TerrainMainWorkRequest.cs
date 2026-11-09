namespace Game.Terrains.Execution;

/// <summary>A durable foreground request that other foreground operations cannot consume.</summary>
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

    /// <summary>Releases this request's single access lease while keeping its worker pause pending.</summary>
    public void YieldAccess() => executor.YieldAccess(this);

    public void Dispose() => executor.Cancel(this);
}
