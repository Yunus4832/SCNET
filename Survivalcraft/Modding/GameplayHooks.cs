using EntitySystem.Core;

namespace Game.Modding;

public interface IModGameplayHooks
{
    IDisposable OnCreatureInjuring(Action<CreatureInjuringContext> handler, int priority = 0);

    IDisposable OnMinerDigging(Action<MinerDiggingContext> handler, int priority = 0);

    IDisposable OnBlockPlacing(Action<BlockPlacingContext> handler, int priority = 0);

    IDisposable OnTerrainCellChanging(Action<TerrainCellChangingContext> handler, int priority = 0);

    IDisposable OnEntityAdded(Action<EntityAddedContext> handler, int priority = 0);

    IDisposable OnWorldUpdating(Action<WorldUpdatingContext> handler, int priority = 0);
}

public sealed class GameplayHooks
{
    private readonly ModHook<CreatureInjuringContext> _creatureInjuring = new();
    private readonly ModHook<MinerDiggingContext> _minerDigging = new();
    private readonly ModHook<BlockPlacingContext> _blockPlacing = new();
    private readonly ModHook<TerrainCellChangingContext> _terrainCellChanging = new();
    private readonly ModHook<EntityAddedContext> _entityAdded = new();
    private readonly ModHook<WorldUpdatingContext> _worldUpdating = new();

    public void Invoke(CreatureInjuringContext context) => _creatureInjuring.Invoke(context);

    public void Invoke(MinerDiggingContext context) => _minerDigging.Invoke(context);

    public void Invoke(BlockPlacingContext context) => _blockPlacing.Invoke(context);

    public void Invoke(TerrainCellChangingContext context) => _terrainCellChanging.Invoke(context);

    public void Invoke(EntityAddedContext context) => _entityAdded.Invoke(context);

    public void Invoke(WorldUpdatingContext context) => _worldUpdating.Invoke(context);

    internal IModGameplayHooks ForOwner(ModId owner) => new OwnedGameplayHooks(owner, this);

    internal void Freeze()
    {
        _creatureInjuring.Freeze();
        _minerDigging.Freeze();
        _blockPlacing.Freeze();
        _terrainCellChanging.Freeze();
        _entityAdded.Freeze();
        _worldUpdating.Freeze();
    }

    internal void RemoveOwner(ModId owner)
    {
        _creatureInjuring.RemoveOwner(owner);
        _minerDigging.RemoveOwner(owner);
        _blockPlacing.RemoveOwner(owner);
        _terrainCellChanging.RemoveOwner(owner);
        _entityAdded.RemoveOwner(owner);
        _worldUpdating.RemoveOwner(owner);
    }

    private sealed class OwnedGameplayHooks(ModId owner, GameplayHooks hooks) : IModGameplayHooks
    {
        public IDisposable OnCreatureInjuring(Action<CreatureInjuringContext> handler, int priority = 0) =>
            hooks._creatureInjuring.Register(owner, handler, priority);

        public IDisposable OnMinerDigging(Action<MinerDiggingContext> handler, int priority = 0) =>
            hooks._minerDigging.Register(owner, handler, priority);

        public IDisposable OnBlockPlacing(Action<BlockPlacingContext> handler, int priority = 0) =>
            hooks._blockPlacing.Register(owner, handler, priority);

        public IDisposable OnTerrainCellChanging(Action<TerrainCellChangingContext> handler, int priority = 0) =>
            hooks._terrainCellChanging.Register(owner, handler, priority);

        public IDisposable OnEntityAdded(Action<EntityAddedContext> handler, int priority = 0) =>
            hooks._entityAdded.Register(owner, handler, priority);

        public IDisposable OnWorldUpdating(Action<WorldUpdatingContext> handler, int priority = 0) =>
            hooks._worldUpdating.Register(owner, handler, priority);
    }
}

internal sealed class ModHook<TContext>
{
    private readonly List<Registration> _registrations = [];
    private Registration[] _handlers = [];
    private bool _isFrozen;
    private long _sequence;

    public IDisposable Register(ModId owner, Action<TContext> handler, int priority)
    {
        ArgumentNullException.ThrowIfNull(handler);
        if (_isFrozen)
        {
            throw new InvalidOperationException("Gameplay hooks are frozen.");
        }

        var registration = new Registration(this, owner, handler, priority, _sequence++);
        _registrations.Add(registration);
        return registration;
    }

    public void Invoke(TContext context)
    {
        foreach (var registration in _handlers)
        {
            try
            {
                registration.Handler(context);
            }
            catch (Exception exception)
            {
                Log.Error($"Mod {registration.Owner} gameplay hook failed: {exception}");
            }
        }
    }

    public void Freeze()
    {
        _isFrozen = true;
        RebuildHandlers();
    }

    public void RemoveOwner(ModId owner)
    {
        _registrations.RemoveAll(registration => registration.Owner == owner);
        RebuildHandlers();
    }

    private void Remove(Registration registration)
    {
        _registrations.Remove(registration);
        RebuildHandlers();
    }

    private void RebuildHandlers()
    {
        _handlers = _registrations
            .OrderByDescending(registration => registration.Priority)
            .ThenBy(registration => registration.Sequence)
            .ToArray();
    }

    private sealed class Registration(
        ModHook<TContext> hook,
        ModId owner,
        Action<TContext> handler,
        int priority,
        long sequence) : IDisposable
    {
        private ModHook<TContext>? _hook = hook;

        public ModId Owner { get; } = owner;
        public Action<TContext> Handler { get; } = handler;
        public int Priority { get; } = priority;
        public long Sequence { get; } = sequence;

        public void Dispose() => Interlocked.Exchange(ref _hook, null)?.Remove(this);
    }
}

public sealed class CreatureInjuringContext(
    ComponentHealth health,
    float amount,
    ComponentCreature? attacker,
    bool ignoreInvulnerability,
    string cause)
{
    public ComponentHealth Health { get; } = health;
    public float Amount { get; set; } = amount;
    public ComponentCreature? Attacker { get; set; } = attacker;
    public bool IgnoreInvulnerability { get; set; } = ignoreInvulnerability;
    public string Cause { get; set; } = cause;
    public bool Cancel { get; set; }
}

public sealed class MinerDiggingContext(
    ComponentMiner miner,
    TerrainRaycastResult raycastResult,
    int cellValue,
    int toolValue)
{
    public ComponentMiner Miner { get; } = miner;
    public TerrainRaycastResult RaycastResult { get; } = raycastResult;
    public int CellValue { get; } = cellValue;
    public int ToolValue { get; } = toolValue;
    public float DigTimeMultiplier { get; set; } = 1f;
    public bool Cancel { get; set; }
}

public sealed class BlockPlacingContext(ComponentMiner miner, TerrainRaycastResult raycastResult, int value)
{
    public ComponentMiner Miner { get; } = miner;
    public TerrainRaycastResult RaycastResult { get; } = raycastResult;
    public int Value { get; set; } = value;
    public bool Cancel { get; set; }
}

public sealed class TerrainCellChangingContext(
    SubsystemTerrain terrain,
    int x,
    int y,
    int z,
    int oldValue,
    int newValue,
    ComponentMiner? miner)
{
    public SubsystemTerrain Terrain { get; } = terrain;
    public int X { get; } = x;
    public int Y { get; } = y;
    public int Z { get; } = z;
    public int OldValue { get; } = oldValue;
    public int NewValue { get; set; } = newValue;
    public ComponentMiner? Miner { get; } = miner;
    public bool Cancel { get; set; }
}

public sealed class EntityAddedContext(Project project, Entity entity)
{
    public Project Project { get; } = project;
    public Entity Entity { get; } = entity;
}

public sealed class WorldUpdatingContext(Project project, float deltaTime)
{
    public Project Project { get; } = project;
    public float DeltaTime { get; } = deltaTime;
}
