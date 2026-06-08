using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public class ComponentInventoryPackage : IPackage
{
    public enum EventType
    {
        ActiveSlotChange, // 活动插槽更改，拿物品的那几个主界面槽位
        InventorySync, // 同步背包

        // 下面2个可以考虑去掉，但是暂时搁置并保留
        QueryErrorInventoryInfo,
        FurnaceSync,
        // =============================

        SetSlotsItem, // 设置背包内的物品
        SetInventory, // 设置整个背包
        HandleMoveItem, // InventorySlotWidget 中的处理函数同名
        HandleDragDrop // InventorySlotWidget 中的处理函数同名
    }

    public int ActiveSlot;

    public DragMode DragMode;

    public EventType PackageEventType;

    public int InventoryID;

    public bool ProcessingOnly;

    public Dictionary<int, List<Slot>> Slots = new();

    public InventorySlot? SourceInventorySlot;

    public Dictionary<IInventory, List<int>> SyncItems = new();

    public InventorySlot? TargetInventorySlot;

    public byte ID => (byte)PackageType.ComponentInventory;

    public Client? To { get; set; }

    public Client? Except { get; set; }

    public Client? From { get; set; }

    public ClientState MinNeedState => ClientState.ProjectLoaded;

    public ComponentInventoryPackage()
    {
    }

    public ComponentInventoryPackage(IInventory inventory, int activeSlot)
    {
        InventoryID = inventory.Id;
        ActiveSlot = activeSlot;
        PackageEventType = EventType.ActiveSlotChange;
    }

    public ComponentInventoryPackage(int inventoryID, EventType eventType)
    {
        InventoryID = inventoryID;
        PackageEventType = eventType;
    }

    public ComponentInventoryPackage(InventorySlot sourceInventorySlot, InventorySlot targetInventorySlot,
        EventType type)
    {
        SourceInventorySlot = sourceInventorySlot;
        TargetInventorySlot = targetInventorySlot;
        PackageEventType = type;
    }

    public ComponentInventoryPackage(InventorySlot inventorySlot)
    {
        SourceInventorySlot = inventorySlot;
        PackageEventType = EventType.SetSlotsItem;
    }

    //客户端/服务器发送包后相关的Inventory的Generation就+1
    //服务器接收到包，判断Inventory的Generation是否为本地+1，为通过验证，不为就驳回并强制同步成服务器的数据
    // isLostSync 如果为true，则返回所有格子的数据??
    public ComponentInventoryPackage(Dictionary<IInventory, List<int>> syncItems)
    {
        Slots = new Dictionary<int, List<Slot>>();
        SyncItems = new Dictionary<IInventory, List<int>>();
        PackageEventType = EventType.InventorySync;

        foreach (var c in syncItems)
        {
            if (!Slots.TryGetValue(c.Key.Id, out var slots))
            {
                slots = new List<Slot>();
                Slots.Add(c.Key.Id, slots);
            }

            if (c.Key is ComponentClothing clothing)
            {
                foreach (var index in c.Value)
                {
                    var slot = new Slot
                    {
                        Type = 1,
                        SlotIndex = index,
                        ClothingList = clothing.GetClothes((ClothingSlot)index).ToList()
                    };
                    var tmp = slots.Find(x => x.SlotIndex == index);
                    if (tmp == null)
                    {
                        slots.Add(slot);
                    }
                    else
                    {
                        var p = slots.IndexOf(tmp);
                        slots[p] = slot;
                    }
                }
            }
            else
            {
                foreach (var index in c.Value)
                {
                    var slot = new Slot
                    {
                        Type = 0,
                        SlotIndex = index,
                        SlotItem = new ComponentInventoryBase.Slot
                        {
                            Count = c.Key.GetSlotCount(index),
                            Value = c.Key.GetSlotValue(index)
                        }
                    };
                    var tmp = slots.Find(x => x.SlotIndex == index);
                    if (tmp == null)
                    {
                        slots.Add(slot);
                    }
                    else
                    {
                        var p = slots.IndexOf(tmp);
                        slots[p] = slot;
                    }
                }
            }
        }
    }

    public void WriteData(PackageStreamWriter writer)
    {
        writer.WriteEnum(PackageEventType);
        switch (PackageEventType)
        {
            case EventType.QueryErrorInventoryInfo:
                writer.Write(InventoryID);
                break;
            case EventType.ActiveSlotChange:
                writer.Write(InventoryID);
                writer.Write(ActiveSlot);
                break;
            case EventType.InventorySync:
                writer.Write(Slots.Count);
                foreach (var item in Slots)
                {
                    writer.Write(item.Key); //inventory id
                    writer.Write(item.Value.Count); //list count
                    foreach (var item2 in item.Value)
                    {
                        writer.Write(item2.SlotIndex);
                        writer.Write(item2.Type);
                        if (item2.Type == 0)
                        {
                            if (item2.SlotItem == null)
                            {
                                continue;
                            }

                            writer.Write(item2.SlotItem.Count);
                            writer.Write(item2.SlotItem.Value);
                        }
                        else
                        {
                            writer.Write((byte)item2.ClothingList.Count);
                            foreach (var item3 in item2.ClothingList)
                            {
                                writer.Write(item3);
                            }
                        }
                    }
                }

                break;
            case EventType.HandleMoveItem:
                if (SourceInventorySlot != null)
                {
                    writer.Write(SourceInventorySlot.InventoryId);
                    writer.Write(SourceInventorySlot.SlotIndex);
                }

                if (TargetInventorySlot != null)
                {
                    writer.Write(TargetInventorySlot.InventoryId);
                    writer.Write(TargetInventorySlot.SlotIndex);
                    writer.Write(TargetInventorySlot.Count);
                }

                break;
            case EventType.HandleDragDrop:
                if (SourceInventorySlot != null)
                {
                    writer.Write(SourceInventorySlot.InventoryId);
                    writer.Write(SourceInventorySlot.SlotIndex);
                }

                writer.WriteEnum(DragMode);
                if (TargetInventorySlot != null)
                {
                    writer.Write(TargetInventorySlot.InventoryId);
                    writer.Write(TargetInventorySlot.SlotIndex);
                }

                writer.Write(ProcessingOnly);
                break;
            case EventType.SetSlotsItem:
                if (SourceInventorySlot != null)
                {
                    writer.Write(SourceInventorySlot.InventoryId);
                    writer.Write(SourceInventorySlot.SlotIndex);
                    writer.Write(SourceInventorySlot.Value);
                    writer.Write(SourceInventorySlot.Count);
                }

                break;
        }
    }

    public void ReadData(PackageStreamReader reader)
    {
        PackageEventType = reader.ReadEnum<EventType>();
        switch (PackageEventType)
        {
            case EventType.QueryErrorInventoryInfo:
                InventoryID = reader.ReadInt32();
                break;
            case EventType.ActiveSlotChange:
                InventoryID = reader.ReadInt32();
                ActiveSlot = reader.ReadInt32();
                break;
            case EventType.InventorySync:
                Slots = new Dictionary<int, List<Slot>>();
                var count = reader.ReadInt32();
                for (var i = 0; i < count; i++)
                {
                    var inventoryId = reader.ReadInt32();
                    var listCount = reader.ReadInt32();
                    var slots = new List<Slot>();
                    for (var k = 0; k < listCount; k++)
                    {
                        var slot = new Slot
                        {
                            SlotIndex = reader.ReadInt32(),
                            Type = reader.ReadByte()
                        };
                        if (slot.Type == 0)
                        {
                            slot.SlotItem = new ComponentInventoryBase.Slot
                                { Count = reader.ReadInt32(), Value = reader.ReadInt32() };
                        }
                        else
                        {
                            slot.ClothingList = new List<int>();
                            int listCount2 = reader.ReadByte();
                            for (var j = 0; j < listCount2; j++)
                            {
                                slot.ClothingList.Add(reader.ReadInt32());
                            }
                        }

                        slots.Add(slot);
                    }

                    Slots.Add(inventoryId, slots);
                }

                break;


            case EventType.HandleMoveItem:
                SourceInventorySlot = new InventorySlot
                {
                    InventoryId = reader.ReadInt32(),
                    SlotIndex = reader.ReadInt32()
                };
                TargetInventorySlot = new InventorySlot
                {
                    InventoryId = reader.ReadInt32(),
                    SlotIndex = reader.ReadInt32(),
                    Count = reader.ReadInt32()
                };
                break;
            case EventType.HandleDragDrop:
                SourceInventorySlot = new InventorySlot
                {
                    InventoryId = reader.ReadInt32(),
                    SlotIndex = reader.ReadInt32()
                };
                DragMode = reader.ReadEnum<DragMode>();
                TargetInventorySlot = new InventorySlot
                {
                    InventoryId = reader.ReadInt32(),
                    SlotIndex = reader.ReadInt32()
                };
                ProcessingOnly = reader.ReadBoolean();
                break;
            case EventType.SetSlotsItem:
            {
                SourceInventorySlot = new InventorySlot
                {
                    InventoryId = reader.ReadInt32(),
                    SlotIndex = reader.ReadInt32(),
                    Value = reader.ReadInt32(),
                    Count = reader.ReadInt32()
                };
            }
                break;
        }
    }


    public class Slot
    {
        public List<int> ClothingList = [];

        public int SlotIndex;

        public ComponentInventoryBase.Slot? SlotItem;

        public byte Type;
    }
}

public class InventorySlot
{
    public int Count;

    public int InventoryId;

    public int SlotIndex;

    public int Value;
}
