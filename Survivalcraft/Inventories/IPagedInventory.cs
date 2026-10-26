namespace Game.Inventories;

public interface IPagedInventory : IInventory
{
    int HotbarSlotsCount { get; }
}
