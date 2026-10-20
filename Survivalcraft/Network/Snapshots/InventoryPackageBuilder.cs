namespace Game.Network.Snapshots;

public static class InventoryPackageBuilder
{
    public static ComponentInventoryPackage Capture(IReadOnlyDictionary<IInventory, List<int>> inventories)
    {
        var groups = new Dictionary<int, List<InventorySlotSnapshot>>();
        foreach (var (inventory, indices) in inventories)
        {
            if (!groups.TryGetValue(inventory.Id, out var slots)) { slots = []; groups.Add(inventory.Id, slots); }
            foreach (var index in indices)
            {
                var state = inventory is ComponentClothing clothing
                    ? new InventorySlotSnapshot { Type = 1, SlotIndex = index, ClothingList = clothing.GetClothes((ClothingSlot)index).ToArray() }
                    : new InventorySlotSnapshot { Type = 0, SlotIndex = index, Count = inventory.GetSlotCount(index), Value = inventory.GetSlotValue(index) };
                var existing = slots.FindIndex(value => value.SlotIndex == index);
                if (existing < 0) { slots.Add(state); } else { slots[existing] = state; }
            }
        }
        return new ComponentInventoryPackage
        {
            PackageEventType = ComponentInventoryPackage.EventType.InventorySync,
            Slots = groups.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<InventorySlotSnapshot>)pair.Value.ToArray())
        };
    }
}
