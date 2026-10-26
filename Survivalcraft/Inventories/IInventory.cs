using EntitySystem.Core;

namespace Game.Inventories;

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
    ///     获取某个物品的容量
    /// </summary>
    /// <param name="slotIndex"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    int GetSlotCapacity(int slotIndex, int value);

    /// <summary>
    ///     获取当某个物品拖向另一个物品的容量
    /// </summary>
    /// <param name="slotIndex"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    int GetSlotProcessCapacity(int slotIndex, int value);

    void AddSlotItems(int slotIndex, int value, int count);

    bool AddNetSlotItems(int slotIndex, int value, int count);

    /// <summary>
    ///     当某个物品拖向另一个物品时执行
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
