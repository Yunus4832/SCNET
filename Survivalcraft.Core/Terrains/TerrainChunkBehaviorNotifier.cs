using Game.Terrains.Distribution;

namespace Game.Terrains;

/// <summary>Delivers chunk business notifications on the owning world's update thread.</summary>
public sealed class TerrainChunkBehaviorNotifier(
    SubsystemTerrain terrain,
    SubsystemBlockBehaviors behaviors,
    TimeProvider? timeProvider = null
)
{
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;

    private volatile Initialization? _initialization;

    public TerrainChunk? PendingChunk => _initialization?.Chunk;

    private sealed class Initialization(
        TerrainChunk chunk,
        bool isLoaded,
        Action<TerrainChunk>[] handlers,
        SubsystemBlockBehavior[] chunkBehaviors
    )
    {
        public TerrainChunk Chunk { get; } = chunk;

        public bool IsLoaded { get; } = isLoaded;

        public Action<TerrainChunk>[] Handlers { get; } = handlers;

        public SubsystemBlockBehavior[] ChunkBehaviors { get; } = chunkBehaviors;

        public int HandlerIndex;

        public int ChunkBehaviorIndex;

        public int X;

        public int Y;

        public int Z;

        public int Value;

        public SubsystemBlockBehavior[]? CellBehaviors;

        public int CellBehaviorIndex;

        public bool Running;

        public void NextCell()
        {
            CellBehaviors = null;
            CellBehaviorIndex = 0;
            if (++Y < 255) { return; }

            Y = 0;
            if (++Z < 16) { return; }

            Z = 0;
            X++;
        }
    }

    /// <summary>Resumes one chunk's ordered callbacks within the caller's foreground budget.</summary>
    public bool TryInitialize(
        TerrainChunk chunk,
        TimeSpan maximumTime,
        Action<TerrainChunk>? onInitializing = null
    )
    {
        ArgumentNullException.ThrowIfNull(chunk);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumTime, TimeSpan.Zero);
        if (!CanContinue(chunk))
        {
            if (ReferenceEquals(PendingChunk, chunk)) { _initialization = null; }

            return false;
        }

        if (maximumTime == TimeSpan.Zero) { return false; }

        RequireOwnedChunk(chunk);
        if (terrain.Access.IsHeldByCurrentThread)
        {
            throw new InvalidOperationException(
                "Chunk initialization callbacks require terrain access to be released."
            );
        }

        if (_initialization is { } active && (!ReferenceEquals(active.Chunk, chunk) || active.Running))
        {
            return false;
        }

        if (_initialization == null && (!chunk.HasCurrentLight || chunk.AreBehaviorsNotified)) { return false; }

        var started = _time.GetTimestamp();
        using var batch = terrain.ContentRole == TerrainContentRole.Authority
            ? terrain.WorkExecutor.CreateMainRequest()
            : null;
        if (batch != null)
        {
            // Allow a short worker handoff without losing the entire frame's initialization turn.
            if (!batch.TryEnter(1)) { return false; }

            // Pause the worker only for this slice, allowing callbacks to acquire terrain access.
            batch.YieldAccess();
            if (!CanContinue(chunk))
            {
                _initialization = null;
                return false;
            }
        }

        if (_initialization == null)
        {
            if (!chunk.HasCurrentLight || chunk.AreBehaviorsNotified) { return false; }

            var isLoaded = chunk.ContentOrigin switch
            {
                TerrainContentOrigin.Generated => false,
                TerrainContentOrigin.Storage or TerrainContentOrigin.Replica => true,
                _ => throw new InvalidOperationException("Initialized terrain contents must have a known origin.")
            };
            _initialization = new Initialization(
                chunk,
                isLoaded,
                onInitializing?.GetInvocationList().Cast<Action<TerrainChunk>>().ToArray() ?? [],
                behaviors.BlockBehaviors.ToArray()
            );
            // Mark before any callback, including reentrant initialization requests.
            chunk.AreBehaviorsNotified = true;
        }

        var progress = _initialization;
        progress.Running = true;
        try
        {
            if (_time.GetElapsedTime(started) >= maximumTime) { return false; }

            var complete = Advance(progress, started, maximumTime);
            if (complete || !CanContinue(chunk)) { _initialization = null; }

            if (complete)
            {
                terrain.Computations?.Request(chunk);
                terrain.WorkExecutor.Wake();
            }

            return complete;
        }
        catch (Exception failure)
        {
            _initialization = null;
            // Replaying partially executed callbacks would duplicate their business side effects.
            try { terrain.Project.Dispose(); }
            catch (Exception closingFailure) { throw new AggregateException(failure, closingFailure); }

            throw;
        }
        finally { progress.Running = false; }
    }

    /// <summary>Finishes the active normalization before a consistent world snapshot is captured.</summary>
    public void CompletePendingInitialization()
    {
        if (_initialization is not { } progress) { return; }

        if (progress.Running || terrain.Access.IsHeldByCurrentThread)
        {
            throw new InvalidOperationException(
                "Cannot capture a world inside a chunk initialization callback or terrain lease."
            );
        }

        using var batch = terrain.ContentRole == TerrainContentRole.Authority
            ? terrain.WorkExecutor.CreateMainRequest()
            : null;
        if (batch != null)
        {
            if (!batch.TryEnter(Timeout.Infinite)) { return; }

            batch.YieldAccess();
        }

        while (_initialization != null)
        {
            TryInitialize(progress.Chunk, TimeSpan.MaxValue);
        }
    }

    private bool Advance(Initialization progress, long started, TimeSpan maximumTime)
    {
        var chunk = progress.Chunk;
        while (progress.HandlerIndex < progress.Handlers.Length)
        {
            progress.Handlers[progress.HandlerIndex++](chunk);
            if (!CanContinue(chunk) || _time.GetElapsedTime(started) >= maximumTime) { return false; }
        }

        while (progress.ChunkBehaviorIndex < progress.ChunkBehaviors.Length)
        {
            progress.ChunkBehaviors[progress.ChunkBehaviorIndex++].OnChunkInitialized(chunk);
            if (!CanContinue(chunk) || _time.GetElapsedTime(started) >= maximumTime) { return false; }
        }

        var scanned = 0;
        while (progress.X < 16)
        {
            if (progress.CellBehaviors == null)
            {
                progress.Value = chunk.GetCellValueFast(progress.X, progress.Y, progress.Z);
                var contents = Terrain.ExtractContents(progress.Value);
                progress.CellBehaviors = contents == 0 ? [] : behaviors.GetBlockBehaviors(contents);
            }

            while (progress.CellBehaviorIndex < progress.CellBehaviors.Length)
            {
                progress.CellBehaviors[progress.CellBehaviorIndex++].OnBlockGenerated(
                    progress.Value,
                    chunk.Origin.X + progress.X,
                    progress.Y,
                    chunk.Origin.Y + progress.Z,
                    progress.IsLoaded
                );
                if (!CanContinue(chunk) || _time.GetElapsedTime(started) >= maximumTime) { return false; }
            }

            progress.NextCell();
            if ((++scanned & 63) == 0 && _time.GetElapsedTime(started) >= maximumTime) { return false; }
        }

        WorldRuleHooks.For(terrain.Project).Invoke(new TerrainChunkInitializedContext(terrain, chunk));
        if (!CanContinue(chunk)) { return false; }

        chunk.IsContentInitialized = true;
        return true;
    }

    /// <summary>Only delivers notifications; the residency owner handles reservation and retry deduplication.</summary>
    public bool NotifyDiscarding(TerrainChunk chunk, Action<TerrainChunk>? onDiscarding = null)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        if (!CanContinue(chunk)) { return false; }

        RequireOwnedChunk(chunk);
        if (terrain.Access.IsHeldByCurrentThread)
        {
            throw new InvalidOperationException("Chunk discard callbacks require terrain access to be released.");
        }

        try
        {
            if (!InvokeCallbacks(onDiscarding, chunk)) { return false; }

            foreach (var behavior in behaviors.BlockBehaviors)
            {
                behavior.OnChunkDiscarding(chunk);
                if (!CanContinue(chunk)) { return false; }
            }

            WorldRuleHooks.For(terrain.Project).Invoke(new TerrainChunkDiscardingContext(terrain, chunk));
            return CanContinue(chunk);
        }
        catch (Exception failure)
        {
            try { terrain.Project.Dispose(); }
            catch (Exception closingFailure) { throw new AggregateException(failure, closingFailure); }

            throw;
        }
    }

    private bool InvokeCallbacks(Action<TerrainChunk>? callbacks, TerrainChunk chunk)
    {
        foreach (var callback in callbacks?.GetInvocationList() ?? [])
        {
            ((Action<TerrainChunk>)callback)(chunk);
            if (!CanContinue(chunk)) { return false; }
        }

        return CanContinue(chunk);
    }

    private bool CanContinue(TerrainChunk chunk)
    {
        return !chunk.IsDisposed && WorldRuntimeContext.IsProjectActive(terrain.Project);
    }

    private void RequireOwnedChunk(TerrainChunk chunk)
    {
        if (!ReferenceEquals(chunk.Terrain, terrain.Terrain))
        {
            throw new ArgumentException("Chunk notifications require the owning world terrain.", nameof(chunk));
        }
    }
}
