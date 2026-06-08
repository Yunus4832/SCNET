using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public partial class BlockEditPackage : IPackage
{
    public enum EventType
    {
        OpenInventoryByID, // 打开背包，通过背包id， 注：背包指的是IInventory
        OpenInventoryByPoint, // 打开背包，通过位置，通常是BlockEntity，例如箱子，炉子
        CrossbowPull, // 十字弩拉弓
        EditSign // 编辑牌子
    }

    public int InventoryId;

    public Point3 Point3;

    public int SlotIndex;

    public EventType Type;

    public byte ID => (byte)PackageType.BlockEdit;

    public Client? To { get; set; }

    public Client? Except { get; set; }

    public Client? From { get; set; }

    public ClientState MinNeedState => ClientState.ProjectLoaded;

    public BlockEditPackage()
    {
    }

    public BlockEditPackage(IInventory inventory)
    {
        InventoryId = inventory.Id;
        Type = EventType.OpenInventoryByID;
    }

    public BlockEditPackage(Point3 point3, EventType type)
    {
        Point3 = point3;
        Type = type;
    }

    public BlockEditPackage(IInventory inventory, int slotIndex, EventType type)
    {
        InventoryId = inventory.Id;
        SlotIndex = slotIndex;
        Type = type;
    }


    public void WriteData(PackageStreamWriter writer)
    {
        writer.WriteEnum(Type);
        switch (Type)
        {
            case EventType.OpenInventoryByID:
                writer.Write(InventoryId);
                break;
            case EventType.OpenInventoryByPoint:
                writer.Write(Point3);
                break;
            case EventType.CrossbowPull:
                writer.Write(InventoryId);
                writer.Write(SlotIndex);
                break;
            case EventType.EditSign:
                writer.Write(Point3);
                break;
        }
    }

    public void ReadData(PackageStreamReader reader)
    {
        Type = reader.ReadEnum<EventType>();
        switch (Type)
        {
            case EventType.OpenInventoryByID:
                InventoryId = reader.ReadInt32();
                break;
            case EventType.OpenInventoryByPoint:
                Point3 = reader.ReadPoint3();
                break;
            case EventType.CrossbowPull:
                InventoryId = reader.ReadInt32();
                SlotIndex = reader.ReadInt32();
                break;
            case EventType.EditSign:
                Point3 = reader.ReadPoint3();
                break;
        }
    }


}
