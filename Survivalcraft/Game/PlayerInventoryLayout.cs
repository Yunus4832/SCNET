using Game.Components;

namespace Game;

public static class PlayerInventoryLayout
{
    public const int RegularHotbarSlotsCount = 14;

    public const int RegularBackpackStartIndex = RegularHotbarSlotsCount;

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
