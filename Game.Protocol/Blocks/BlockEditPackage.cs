using Engine.Core.Mathematics;

namespace Game.Protocol.Blocks;

public sealed record BlockEditPackage : IProtocolMessage
{
    public enum EventType
    {
        OpenInventoryByID = 0, // 打开背包，通过背包id， 注：背包指的是IInventory

        OpenInventoryByPoint = 1, // 打开背包，通过位置，通常是BlockEntity，例如箱子，炉子

        CrossbowPull = 2, // 十字弩拉弓

        EditSign = 3 // 编辑牌子
    }

    public int InventoryId { get; init; }

    public Point3 Point3 { get; init; }

    public int SlotIndex { get; init; }

    public EventType Type { get; init; }

    public byte ID => (byte)PackageType.BlockEdit;

    public BlockEditPackage()
    {
    }

    public BlockEditPackage(int inventoryId)
    {
        InventoryId = inventoryId;
        Type = EventType.OpenInventoryByID;
    }

    public BlockEditPackage(Point3 point3, EventType type)
    {
        Point3 = point3;
        Type = type;
    }

    public BlockEditPackage(int inventoryId, int slotIndex, EventType type)
    {
        InventoryId = inventoryId;
        SlotIndex = slotIndex;
        Type = type;
    }
}
