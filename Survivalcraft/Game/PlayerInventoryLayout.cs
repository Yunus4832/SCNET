using Game.Components;

namespace Game;

public static class PlayerInventoryLayout
{
    public const int BackpackSlotsCount = 16;

    public static int GetBackpackStartIndex(IInventory inventory)
    {
        return inventory switch
        {
            ComponentCreativeInventory creativeInventory => creativeInventory.BackpackStartIndex,
            ComponentInventory inventoryComponent => inventoryComponent.BackpackStartIndex,
            _ => throw new InvalidOperationException("Inventory is not a player inventory.")
        };
    }
}
