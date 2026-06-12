namespace Game.Modding;

public interface IModBlockBehaviorHooks
{
    IDisposable OnBlockAdded(Action<BlockAddedContext> handler, int priority = 0);

    IDisposable OnBlockRemoved(Action<BlockRemovedContext> handler, int priority = 0);

    IDisposable OnBlockModified(Action<BlockModifiedContext> handler, int priority = 0);

    IDisposable OnNeighborBlockChanged(Action<NeighborBlockChangedContext> handler, int priority = 0);

    IDisposable OnUse(Action<BlockUseContext> handler, int priority = 0);

    IDisposable OnInteract(Action<BlockInteractContext> handler, int priority = 0);

    IDisposable OnBlockPlaced(Action<BlockPlacedContext> handler, int priority = 0);

    IDisposable OnEditBlock(Action<BlockEditContext> handler, int priority = 0);

    IDisposable OnEditInventoryItem(Action<BlockEditInventoryItemContext> handler, int priority = 0);

    IDisposable OnItemHarvested(Action<ItemHarvestedContext> handler, int priority = 0);
}

public sealed class BlockBehaviorHooks
{
    private readonly ModHook<BlockAddedContext> _blockAdded = new();
    private readonly ModHook<BlockRemovedContext> _blockRemoved = new();
    private readonly ModHook<BlockModifiedContext> _blockModified = new();
    private readonly ModHook<NeighborBlockChangedContext> _neighborBlockChanged = new();
    private readonly ModHook<BlockUseContext> _blockUse = new();
    private readonly ModHook<BlockInteractContext> _blockInteract = new();
    private readonly ModHook<BlockPlacedContext> _blockPlaced = new();
    private readonly ModHook<BlockEditContext> _blockEdit = new();
    private readonly ModHook<BlockEditInventoryItemContext> _blockEditInventoryItem = new();
    private readonly ModHook<ItemHarvestedContext> _itemHarvested = new();

    public void Invoke(BlockAddedContext context) => _blockAdded.Invoke(context);

    public void Invoke(BlockRemovedContext context) => _blockRemoved.Invoke(context);

    public void Invoke(BlockModifiedContext context) => _blockModified.Invoke(context);

    public void Invoke(NeighborBlockChangedContext context) => _neighborBlockChanged.Invoke(context);

    public void Invoke(BlockUseContext context) => _blockUse.Invoke(context);

    public void Invoke(BlockInteractContext context) => _blockInteract.Invoke(context);

    public void Invoke(BlockPlacedContext context) => _blockPlaced.Invoke(context);

    public void Invoke(BlockEditContext context) => _blockEdit.Invoke(context);

    public void Invoke(BlockEditInventoryItemContext context) => _blockEditInventoryItem.Invoke(context);

    public void Invoke(ItemHarvestedContext context) => _itemHarvested.Invoke(context);

    internal IModBlockBehaviorHooks ForOwner(ModId owner) => new OwnedBlockBehaviorHooks(owner, this);

    internal void Freeze()
    {
        _blockAdded.Freeze();
        _blockRemoved.Freeze();
        _blockModified.Freeze();
        _neighborBlockChanged.Freeze();
        _blockUse.Freeze();
        _blockInteract.Freeze();
        _blockPlaced.Freeze();
        _blockEdit.Freeze();
        _blockEditInventoryItem.Freeze();
        _itemHarvested.Freeze();
    }

    internal void RemoveOwner(ModId owner)
    {
        _blockAdded.RemoveOwner(owner);
        _blockRemoved.RemoveOwner(owner);
        _blockModified.RemoveOwner(owner);
        _neighborBlockChanged.RemoveOwner(owner);
        _blockUse.RemoveOwner(owner);
        _blockInteract.RemoveOwner(owner);
        _blockPlaced.RemoveOwner(owner);
        _blockEdit.RemoveOwner(owner);
        _blockEditInventoryItem.RemoveOwner(owner);
        _itemHarvested.RemoveOwner(owner);
    }

    private sealed class OwnedBlockBehaviorHooks(ModId owner, BlockBehaviorHooks hooks) : IModBlockBehaviorHooks
    {
        public IDisposable OnBlockAdded(Action<BlockAddedContext> handler, int priority = 0) =>
            hooks._blockAdded.Register(owner, handler, priority);

        public IDisposable OnBlockRemoved(Action<BlockRemovedContext> handler, int priority = 0) =>
            hooks._blockRemoved.Register(owner, handler, priority);

        public IDisposable OnBlockModified(Action<BlockModifiedContext> handler, int priority = 0) =>
            hooks._blockModified.Register(owner, handler, priority);

        public IDisposable OnNeighborBlockChanged(Action<NeighborBlockChangedContext> handler, int priority = 0) =>
            hooks._neighborBlockChanged.Register(owner, handler, priority);

        public IDisposable OnUse(Action<BlockUseContext> handler, int priority = 0) =>
            hooks._blockUse.Register(owner, handler, priority);

        public IDisposable OnInteract(Action<BlockInteractContext> handler, int priority = 0) =>
            hooks._blockInteract.Register(owner, handler, priority);

        public IDisposable OnBlockPlaced(Action<BlockPlacedContext> handler, int priority = 0) =>
            hooks._blockPlaced.Register(owner, handler, priority);

        public IDisposable OnEditBlock(Action<BlockEditContext> handler, int priority = 0) =>
            hooks._blockEdit.Register(owner, handler, priority);

        public IDisposable OnEditInventoryItem(Action<BlockEditInventoryItemContext> handler, int priority = 0) =>
            hooks._blockEditInventoryItem.Register(owner, handler, priority);

        public IDisposable OnItemHarvested(Action<ItemHarvestedContext> handler, int priority = 0) =>
            hooks._itemHarvested.Register(owner, handler, priority);
    }
}

public sealed class BlockAddedContext(
    SubsystemTerrain terrain,
    int x,
    int y,
    int z,
    int value,
    int oldValue,
    ComponentMiner? miner)
{
    public SubsystemTerrain Terrain { get; } = terrain;
    public int X { get; } = x;
    public int Y { get; } = y;
    public int Z { get; } = z;
    public int Value { get; } = value;
    public int OldValue { get; } = oldValue;
    public ComponentMiner? Miner { get; } = miner;
    public bool Cancel { get; set; }
}

public sealed class BlockRemovedContext(
    SubsystemTerrain terrain,
    int x,
    int y,
    int z,
    int value,
    int newValue,
    ComponentMiner? miner)
{
    public SubsystemTerrain Terrain { get; } = terrain;
    public int X { get; } = x;
    public int Y { get; } = y;
    public int Z { get; } = z;
    public int Value { get; } = value;
    public int NewValue { get; } = newValue;
    public ComponentMiner? Miner { get; } = miner;
    public bool Cancel { get; set; }
}

public sealed class BlockModifiedContext(
    SubsystemTerrain terrain,
    int x,
    int y,
    int z,
    int value,
    int oldValue,
    ComponentMiner? miner)
{
    public SubsystemTerrain Terrain { get; } = terrain;
    public int X { get; } = x;
    public int Y { get; } = y;
    public int Z { get; } = z;
    public int Value { get; } = value;
    public int OldValue { get; } = oldValue;
    public ComponentMiner? Miner { get; } = miner;
    public bool Cancel { get; set; }
}

public sealed class NeighborBlockChangedContext(
    SubsystemTerrain terrain,
    int x,
    int y,
    int z,
    int neighborX,
    int neighborY,
    int neighborZ,
    ComponentMiner? miner)
{
    public SubsystemTerrain Terrain { get; } = terrain;
    public int X { get; } = x;
    public int Y { get; } = y;
    public int Z { get; } = z;
    public int NeighborX { get; } = neighborX;
    public int NeighborY { get; } = neighborY;
    public int NeighborZ { get; } = neighborZ;
    public ComponentMiner? Miner { get; } = miner;
    public int Value { get; set; } = terrain.Terrain.GetCellValue(x, y, z);
    public int NeighborValue { get; set; } = terrain.Terrain.GetCellValue(neighborX, neighborY, neighborZ);
    public bool Cancel { get; set; }
}

public sealed class BlockUseContext(
    Ray3 ray,
    ComponentMiner componentMiner,
    int blockValue)
{
    public Ray3 Ray { get; } = ray;
    public ComponentMiner Miner { get; } = componentMiner;
    public int BlockValue { get; } = blockValue;
    public bool Cancel { get; set; }
    public bool Handled { get; set; }
}

public sealed class BlockInteractContext(
    TerrainRaycastResult raycastResult,
    ComponentMiner componentMiner,
    int cellValue)
{
    public TerrainRaycastResult RaycastResult { get; } = raycastResult;
    public ComponentMiner Miner { get; } = componentMiner;
    public int CellValue { get; } = cellValue;
    public bool Cancel { get; set; }
    public bool Handled { get; set; }
}

public sealed class BlockPlacedContext(
    ComponentMiner miner,
    int x,
    int y,
    int z,
    BlockPlacementData placementData,
    int value)
{
    public ComponentMiner Miner { get; } = miner;
    public int X { get; } = x;
    public int Y { get; } = y;
    public int Z { get; } = z;
    public BlockPlacementData PlacementData { get; set; } = placementData;
    public int Value { get; set; } = value;
    public bool Cancel { get; set; }
}

public sealed class BlockEditContext(
    int x,
    int y,
    int z,
    int value,
    ComponentPlayer componentPlayer)
{
    public int X { get; } = x;
    public int Y { get; } = y;
    public int Z { get; } = z;
    public int Value { get; } = value;
    public ComponentPlayer ComponentPlayer { get; } = componentPlayer;
    public bool Cancel { get; set; }
    public bool Handled { get; set; }
}

public sealed class BlockEditInventoryItemContext(
    IInventory inventory,
    int slotIndex,
    ComponentPlayer componentPlayer)
{
    public IInventory Inventory { get; } = inventory;
    public int SlotIndex { get; } = slotIndex;
    public ComponentPlayer ComponentPlayer { get; } = componentPlayer;
    public bool Cancel { get; set; }
    public bool Handled { get; set; }
}

public sealed class ItemHarvestedContext(
    SubsystemTerrain terrain,
    int x,
    int y,
    int z,
    int blockValue,
    BlockDropValue dropValue,
    int newBlockValue)
{
    public SubsystemTerrain Terrain { get; } = terrain;
    public int X { get; } = x;
    public int Y { get; } = y;
    public int Z { get; } = z;
    public int BlockValue { get; } = blockValue;
    public BlockDropValue DropValue { get; set; } = dropValue;
    public int NewBlockValue { get; set; } = newBlockValue;
    public bool Cancel { get; set; }
}
