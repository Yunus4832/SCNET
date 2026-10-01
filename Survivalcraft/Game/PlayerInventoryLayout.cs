namespace Game;

public static class PlayerInventoryLayout
{
    public const int MinHotbarSlotsCount = 7;

    public const int MaxHotbarSlotsCount = 30;

    public const int BackpackStartIndex = MaxHotbarSlotsCount;

    public const int BackpackSlotsCount = 16;

    public const int StorageSlotsCount = BackpackStartIndex + BackpackSlotsCount;

    public static int GetBackpackStartIndex(IInventory inventory)
    {
        if (inventory is not IPagedInventory)
        {
            throw new InvalidOperationException("Inventory is not a player inventory.");
        }

        return BackpackStartIndex;
    }
}
