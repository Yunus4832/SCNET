using EntitySystem.Core;

namespace Game;

public interface IInventory
{
    Project Project { get; }

    int SlotsCount { get; }

    int Id { get; }

    int VisibleSlotsCount { get; set; }

    int ActiveSlotIndex { get; set; }

    void SetSlotValue(int slotIndex, object obj);

    int GetSlotValue(int slotIndex);

    int GetSlotCount(int slotIndex);

    void OnSlotChange(int slotIndex);

    /// <summary>
    /// 获取某个物品的容量
    /// </summary>
    /// <param name="slotIndex"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    int GetSlotCapacity(int slotIndex, int value);

    /// <summary>
    /// 获取当某个物品拖向另一个物品的容量
    /// </summary>
    /// <param name="slotIndex"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    int GetSlotProcessCapacity(int slotIndex, int value);

    void AddSlotItems(int slotIndex, int value, int count);

    bool AddNetSlotItems(int slotIndex, int value, int count);

    /// <summary>
    /// 当某个物品拖向另一个物品时执行
    /// </summary>
    /// <param name="sourceInventory">源Inventory</param>
    /// <param name="sourceSlotIndex">源slotIndex</param>
    /// <param name="slotIndex">目标slotIndex</param>
    /// <param name="value">源方块值</param>
    /// <param name="count">源方块数量</param>
    /// <param name="processCount"></param>
    /// <param name="processedValue"></param>
    /// <param name="processedCount"></param>
    void ProcessSlotItems(
        IInventory sourceInventory,
        int sourceSlotIndex,
        int slotIndex,
        int value,
        int count,
        int processCount,
        out int processedValue,
        out int processedCount
    );

    int RemoveSlotItems(int slotIndex, int count);

    int RemoveNetSlotItems(int slotIndex, int count);

    void DropAllItems(Vector3 position);

    void DropSlotItems(int slotIndex, Vector3 position, Vector3 velocity);
}

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
