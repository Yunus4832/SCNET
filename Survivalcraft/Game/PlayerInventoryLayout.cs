using Game.Components;

namespace Game;

public static class PlayerInventoryLayout
{
    public const int RegularBackpackStartIndex = ComponentInventory.ShortInventorySlotsCount;

    public const int CreativeBackpackStartIndex = 30;

    public const int BackpackSlotsCount = 16;

    public static int GetBackpackStartIndex(IInventory inventory)
    {
        return inventory switch
        {
            ComponentCreativeInventory => CreativeBackpackStartIndex,
            ComponentInventory => RegularBackpackStartIndex,
            _ => throw new InvalidOperationException("Inventory is not a player inventory.")
        };
    }
}
