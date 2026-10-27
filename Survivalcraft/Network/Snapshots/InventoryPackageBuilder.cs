namespace Game.Network.Snapshots;

public static class InventoryPackageBuilder
{
    public static InventorySnapshotPackage Capture(IReadOnlyDictionary<IInventory, List<int>> inventories)
    {
        var groups = new Dictionary<int, List<InventorySlotSnapshot>>();
        foreach (var (inventory, indices) in inventories)
        {
            if (!groups.TryGetValue(inventory.Id, out var slots))
            {
                slots = [];
                groups.Add(inventory.Id, slots);
            }

            foreach (var index in indices)
            {
                InventorySlotSnapshot state = inventory is ComponentClothing clothing
                    ? new ClothingSlotSnapshot
                    {
                        SlotIndex = index, Clothes = clothing.GetClothes((ClothingSlot)index).ToArray()
                    }
                    : new ItemSlotSnapshot
                    {
                        SlotIndex = index, Count = inventory.GetSlotCount(index),
                        Value = inventory.GetSlotValue(index)
                    };
                var existing = slots.FindIndex(value => value.SlotIndex == index);
                if (existing < 0) { slots.Add(state); }
                else { slots[existing] = state; }
            }
        }

        return new InventorySnapshotPackage
        {
            Slots = groups.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<InventorySlotSnapshot>)pair.Value.ToArray()
            )
        };
    }
}
