using EntitySystem.Core;

namespace Game.Inventories;

public sealed class InventoryDefault : IInventory
{
    public static readonly InventoryDefault Default = new();

    public Project Project => null!;

    public int SlotsCount => -1;

    public int Id => 0;

    public int VisibleSlotsCount { get; set; }

    public int ActiveSlotIndex { get; set; }

    private InventoryDefault()
    {
    }

    public void SetSlotValue(int slotIndex, object obj)
    {
    }

    public int GetSlotValue(int slotIndex)
    {
        return 0;
    }

    public int GetSlotCount(int slotIndex)
    {
        return 0;
    }

    public void OnSlotChange(int slotIndex)
    {
    }

    public int GetSlotCapacity(int slotIndex, int value)
    {
        return 0;
    }

    public int GetSlotProcessCapacity(int slotIndex, int value)
    {
        return 0;
    }

    public void AddSlotItems(int slotIndex, int value, int count)
    {
    }

    public bool AddNetSlotItems(int slotIndex, int value, int count)
    {
        return false;
    }

    public void ProcessSlotItems(
        IInventory sourceInventory,
        int sourceSlotIndex,
        int slotIndex,
        int value,
        int count,
        int processCount,
        out int processedValue,
        out int processedCount
    )
    {
        processedValue = 0;
        processedCount = 0;
    }

    public int RemoveSlotItems(int slotIndex, int count)
    {
        return 0;
    }

    public int RemoveNetSlotItems(int slotIndex, int count)
    {
        return 0;
    }

    public void DropAllItems(Vector3 position)
    {
    }

    public void DropSlotItems(int slotIndex, Vector3 position, Vector3 velocity)
    {
    }
}
