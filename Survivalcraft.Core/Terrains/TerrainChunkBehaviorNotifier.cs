using Game.Terrains.Distribution;

namespace Game.Terrains;

/// <summary>Delivers chunk business notifications on the owning world's update thread.</summary>
public sealed class TerrainChunkBehaviorNotifier(
    SubsystemTerrain terrain,
    SubsystemBlockBehaviors behaviors
)
{
    public bool TryInitialize(TerrainChunk chunk, Action<TerrainChunk>? onInitializing = null)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        if (!CanContinue(chunk) || !chunk.HasCurrentLight || chunk.AreBehaviorsNotified) { return false; }

        RequireOwnedChunk(chunk);
        if (terrain.Access.IsHeldByCurrentThread)
        {
            throw new InvalidOperationException(
                "Chunk initialization callbacks require terrain access to be released."
            );
        }

        using var batch = terrain.ContentRole == TerrainContentRole.Authority
            ? terrain.WorkExecutor.CreateMainRequest()
            : null;
        if (batch != null)
        {
            if (!batch.TryEnter(Timeout.Infinite)) { return false; }

            // Keep normalization writes together without holding access across business callbacks.
            batch.YieldAccess();
            if (!CanContinue(chunk)) { return false; }

            if (!chunk.HasCurrentLight || chunk.AreBehaviorsNotified)
            {
                return false;
            }
        }

        var isLoaded = chunk.ContentOrigin switch
        {
            TerrainContentOrigin.Generated => false,
            TerrainContentOrigin.Storage or TerrainContentOrigin.Replica => true,
            _ => throw new InvalidOperationException("Initialized terrain contents must have a known origin.")
        };
        // Mark before any callback: a callback may reenter initialization or close the world.
        chunk.AreBehaviorsNotified = true;
        try
        {
            if (!InitializeContents(chunk, onInitializing, isLoaded)) { return false; }
        }
        catch (Exception failure)
        {
            // Retrying partially executed business callbacks would duplicate their side effects.
            try { terrain.Project.Dispose(); }
            catch (Exception closingFailure)
            {
                throw new AggregateException(
                    "Chunk initialization and world shutdown failed.",
                    failure,
                    closingFailure
                );
            }

            throw;
        }


        return true;
    }

    private bool InitializeContents(TerrainChunk chunk, Action<TerrainChunk>? onInitializing, bool isLoaded)
    {
        if (!InvokeCallbacks(onInitializing, chunk)) { return false; }

        foreach (var behavior in behaviors.BlockBehaviors)
        {
            behavior.OnChunkInitialized(chunk);
            if (!CanContinue(chunk)) { return false; }
        }

        for (var x = 0; x < 16; x++)
        {
            for (var z = 0; z < 16; z++)
            {
                var index = TerrainChunk.CalculateCellIndex(x, 0, z);
                for (var y = 0; y < 255; y++, index++)
                {
                    var value = chunk.GetCellValueFast(index);
                    var contents = Terrain.ExtractContents(value);
                    if (contents == 0) { continue; }

                    foreach (var behavior in behaviors.GetBlockBehaviors(contents))
                    {
                        behavior.OnBlockGenerated(value, chunk.Origin.X + x, y, chunk.Origin.Y + z, isLoaded);
                        if (!CanContinue(chunk)) { return false; }
                    }
                }
            }
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
