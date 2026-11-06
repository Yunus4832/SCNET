using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

using EntitySystem.Core;

namespace Game.Modding;

/// <summary>World-scoped dispatch through the existing mod host registrations.</summary>
public sealed class WorldRuleHooks : IDisposable
{
    private static readonly ConditionalWeakTable<WorldRuntimeContext, WorldRuleHooks> _bindings = new();

    private readonly GameplayHooks _gameplay;

    private readonly BlockBehaviorHooks _blocks;

    private readonly CancellationTokenRegistration _closed;

    private volatile bool _disabled;

    private bool _disposed;

    public WorldRuntimeContext World { get; }

    public WorldRuleHooks(WorldRuntimeContext world, GameplayHooks gameplay, BlockBehaviorHooks blocks)
    {
        ArgumentNullException.ThrowIfNull(gameplay);
        ArgumentNullException.ThrowIfNull(blocks);
        ObjectDisposedException.ThrowIf(world.IsClosed, world);
        World = world;
        _gameplay = gameplay;
        _blocks = blocks;
        _bindings.Add(world, this);
        try
        {
            world.OwnWorldResource(this);
            _closed = world.Lifetime.Register(() => _disabled = true);
        }
        catch
        {
            _bindings.Remove(world);
            throw;
        }
    }

    public static WorldRuleHooks For(WorldRuntimeContext world)
    {
        return _bindings.TryGetValue(world, out var hooks)
            ? hooks
            : throw new InvalidOperationException("World has no explicitly bound rule hooks.");
    }

    public static WorldRuleHooks For(Project project) => For(WorldRuntimeContext.For(project));

    public static bool TryGet(WorldRuntimeContext world, [NotNullWhen(true)] out WorldRuleHooks? hooks)
    {
        return _bindings.TryGetValue(world, out hooks);
    }

    internal bool HasExplosionPointProcessingHandlers =>
        !_disabled && !World.IsClosed && _gameplay.HasExplosionPointProcessingHandlers;

    internal bool HasTerrainCollisionBoxesHandlers =>
        !_disabled && !World.IsClosed && _gameplay.HasTerrainCollisionBoxesHandlers;

    private bool CanInvoke(IWorldHookContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (_disabled || _disposed || World.IsClosed || context.Project.IsDisposed) { return false; }

        if (!ReferenceEquals(World.Project, context.Project))
        {
            throw new InvalidOperationException("Rule hook context belongs to another world.");
        }

        return true;
    }

    public void Invoke(CreatureInjuringContext context)
    {
        if (CanInvoke(context)) { _gameplay.Invoke(context); }
    }

    public void Invoke(MinerDiggingContext context)
    {
        if (CanInvoke(context)) { _gameplay.Invoke(context); }
    }

    public void Invoke(BlockPlacingContext context)
    {
        if (CanInvoke(context)) { _gameplay.Invoke(context); }
    }

    public void Invoke(TerrainCellChangingContext context)
    {
        if (CanInvoke(context)) { _gameplay.Invoke(context); }
    }

    public void Invoke(EntityAddedContext context)
    {
        if (CanInvoke(context)) { _gameplay.Invoke(context); }
    }

    public void Invoke(WorldUpdatingContext context)
    {
        if (CanInvoke(context)) { _gameplay.Invoke(context); }
    }

    public void Invoke(TerrainChunkGeneratedContext context)
    {
        if (CanInvoke(context)) { _gameplay.Invoke(context); }
    }

    public void Invoke(TerrainChunkInitializedContext context)
    {
        if (CanInvoke(context)) { _gameplay.Invoke(context); }
    }

    public void Invoke(TerrainChunkDiscardingContext context)
    {
        if (CanInvoke(context)) { _gameplay.Invoke(context); }
    }

    public void Invoke(PlayerDyingContext context)
    {
        if (CanInvoke(context)) { _gameplay.Invoke(context); }
    }

    public void Invoke(PlayerSpawnedContext context)
    {
        if (CanInvoke(context)) { _gameplay.Invoke(context); }
    }

    public void Invoke(PlayerRespawnRequestedContext context)
    {
        if (CanInvoke(context)) { _gameplay.Invoke(context); }
    }

    public void Invoke(CreatureTargetScoringContext context)
    {
        if (CanInvoke(context)) { _gameplay.Invoke(context); }
    }

    public void Invoke(CellIgnitingContext context)
    {
        if (CanInvoke(context)) { _gameplay.Invoke(context); }
    }

    public void Invoke(ExplosionPointProcessingContext context)
    {
        if (CanInvoke(context)) { _gameplay.Invoke(context); }
    }

    public void Invoke(TerrainCollisionBoxesContext context)
    {
        if (CanInvoke(context)) { _gameplay.Invoke(context); }
    }

    public void Invoke(MountingContext context)
    {
        if (CanInvoke(context)) { _gameplay.Invoke(context); }
    }

    public void Invoke(PistonBlockMovingContext context)
    {
        if (CanInvoke(context)) { _gameplay.Invoke(context); }
    }

    public void Invoke(MovingBlockSetTerrainCollisionContext context)
    {
        if (CanInvoke(context)) { _gameplay.Invoke(context); }
    }

    public void Invoke(BlockAddedContext context)
    {
        if (CanInvoke(context)) { _blocks.Invoke(context); }
    }

    public void Invoke(BlockRemovedContext context)
    {
        if (CanInvoke(context)) { _blocks.Invoke(context); }
    }

    public void Invoke(BlockModifiedContext context)
    {
        if (CanInvoke(context)) { _blocks.Invoke(context); }
    }

    public void Invoke(NeighborBlockChangedContext context)
    {
        if (CanInvoke(context)) { _blocks.Invoke(context); }
    }

    public void Invoke(BlockUseContext context)
    {
        if (CanInvoke(context)) { _blocks.Invoke(context); }
    }

    public void Invoke(BlockInteractContext context)
    {
        if (CanInvoke(context)) { _blocks.Invoke(context); }
    }

    public void Invoke(BlockPlacedContext context)
    {
        if (CanInvoke(context)) { _blocks.Invoke(context); }
    }

    public void Invoke(BlockEditContext context)
    {
        if (CanInvoke(context)) { _blocks.Invoke(context); }
    }

    public void Invoke(BlockEditInventoryItemContext context)
    {
        if (CanInvoke(context)) { _blocks.Invoke(context); }
    }

    public void Invoke(ItemHarvestedContext context)
    {
        if (CanInvoke(context)) { _blocks.Invoke(context); }
    }

    public void Dispose()
    {
        if (_disposed) { return; }

        _disposed = true;
        _disabled = true;
        _closed.Dispose();
        _bindings.Remove(World);
    }
}
