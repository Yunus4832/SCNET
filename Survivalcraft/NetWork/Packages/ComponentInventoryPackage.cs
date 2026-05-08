namespace Game.NetWork.Packages;

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

    private int _activeSlot;

    public DragMode DragMode;

    private EventType _eventType;

    private int _inventoryID;

    public bool ProcessingOnly;

    public Dictionary<int, List<Slot>> Slots = new();

    private InventorySlot? _sourceInventorySlot;

    public Dictionary<IInventory, List<int>> SyncItems = new();

    private InventorySlot? _targetInventorySlot;

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
        _inventoryID = inventory.Id;
        _activeSlot = activeSlot;
        _eventType = EventType.ActiveSlotChange;
    }

    public ComponentInventoryPackage(int inventoryID, EventType eventType)
    {
        _inventoryID = inventoryID;
        _eventType = eventType;
    }

    public ComponentInventoryPackage(InventorySlot sourceInventorySlot, InventorySlot targetInventorySlot,
        EventType type)
    {
        _sourceInventorySlot = sourceInventorySlot;
        _targetInventorySlot = targetInventorySlot;
        _eventType = type;
    }

    public ComponentInventoryPackage(InventorySlot inventorySlot)
    {
        _sourceInventorySlot = inventorySlot;
        _eventType = EventType.SetSlotsItem;
    }

    //客户端/服务器发送包后相关的Inventory的Generation就+1
    //服务器接收到包，判断Inventory的Generation是否为本地+1，为通过验证，不为就驳回并强制同步成服务器的数据
    // isLostSync 如果为true，则返回所有格子的数据??
    public ComponentInventoryPackage(Dictionary<IInventory, List<int>> syncItems)
    {
        Slots = new Dictionary<int, List<Slot>>();
        SyncItems = new Dictionary<IInventory, List<int>>();
        _eventType = EventType.InventorySync;

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
        writer.WriteEnum(_eventType);
        switch (_eventType)
        {
            case EventType.QueryErrorInventoryInfo:
                writer.Write(_inventoryID);
                break;
            case EventType.ActiveSlotChange:
                writer.Write(_inventoryID);
                writer.Write(_activeSlot);
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
                if (_sourceInventorySlot != null)
                {
                    writer.Write(_sourceInventorySlot.InventoryId);
                    writer.Write(_sourceInventorySlot.SlotIndex);
                }

                if (_targetInventorySlot != null)
                {
                    writer.Write(_targetInventorySlot.InventoryId);
                    writer.Write(_targetInventorySlot.SlotIndex);
                    writer.Write(_targetInventorySlot.Count);
                }

                break;
            case EventType.HandleDragDrop:
                if (_sourceInventorySlot != null)
                {
                    writer.Write(_sourceInventorySlot.InventoryId);
                    writer.Write(_sourceInventorySlot.SlotIndex);
                }

                writer.WriteEnum(DragMode);
                if (_targetInventorySlot != null)
                {
                    writer.Write(_targetInventorySlot.InventoryId);
                    writer.Write(_targetInventorySlot.SlotIndex);
                }

                writer.Write(ProcessingOnly);
                break;
            case EventType.SetSlotsItem:
                if (_sourceInventorySlot != null)
                {
                    writer.Write(_sourceInventorySlot.InventoryId);
                    writer.Write(_sourceInventorySlot.SlotIndex);
                    writer.Write(_sourceInventorySlot.Value);
                    writer.Write(_sourceInventorySlot.Count);
                }

                break;
        }
    }

    public void ReadData(PackageStreamReader reader)
    {
        _eventType = reader.ReadEnum<EventType>();
        switch (_eventType)
        {
            case EventType.QueryErrorInventoryInfo:
                _inventoryID = reader.ReadInt32();
                break;
            case EventType.ActiveSlotChange:
                _inventoryID = reader.ReadInt32();
                _activeSlot = reader.ReadInt32();
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
                _sourceInventorySlot = new InventorySlot
                {
                    InventoryId = reader.ReadInt32(),
                    SlotIndex = reader.ReadInt32()
                };
                _targetInventorySlot = new InventorySlot
                {
                    InventoryId = reader.ReadInt32(),
                    SlotIndex = reader.ReadInt32(),
                    Count = reader.ReadInt32()
                };
                break;
            case EventType.HandleDragDrop:
                _sourceInventorySlot = new InventorySlot
                {
                    InventoryId = reader.ReadInt32(),
                    SlotIndex = reader.ReadInt32()
                };
                DragMode = reader.ReadEnum<DragMode>();
                _targetInventorySlot = new InventorySlot
                {
                    InventoryId = reader.ReadInt32(),
                    SlotIndex = reader.ReadInt32()
                };
                ProcessingOnly = reader.ReadBoolean();
                break;
            case EventType.SetSlotsItem:
            {
                _sourceInventorySlot = new InventorySlot
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

    public void Handle(ProjectNet projectNet, NetNode netNode, bool isServer)
    {
        var subsystemInventories = projectNet.FindSubsystem<SubsystemInventories>();

        IInventory? sourceInventoryObject;
        IInventory? targetInventoryObject;

        switch (_eventType)
        {
            case EventType.ActiveSlotChange:
                subsystemInventories?.FindInventoryById(_inventoryID, inventory =>
                {
                    inventory.ActiveSlotIndex = _activeSlot;
                    if (!isServer)
                    {
                        return;
                    }

                    Except = From;
                    netNode.QueuePackage(this);
                });
                break;
            case EventType.InventorySync:
                // bool isLostSync = false;
                if (isServer)
                {
                }
                else
                {
                    //客户端接受服务器的包
                    foreach (var item in Slots)
                    {
                        subsystemInventories?.FindInventoryById(item.Key, inventory =>
                        {
                            foreach (var item2 in item.Value)
                            {
                                if (item2.Type == 0)
                                {
                                    if (item2.SlotItem != null)
                                    {
                                        inventory.SetSlotValue(item2.SlotIndex, item2.SlotItem);
                                    }
                                }
                                else
                                {
                                    inventory.SetSlotValue(item2.SlotIndex, item2.ClothingList);
                                }
                            }
                        });
                    }
                }

                break;
            case EventType.QueryErrorInventoryInfo:
                if (isServer)
                {
                    subsystemInventories?.FindInventoryById(_inventoryID, inventory =>
                    {
                        var extra = "";
                        if (inventory is ComponentCraftingTable t)
                        {
                            extra = t.Entity.ValuesDictionary.DatabaseObject.Name;
                        }

                        Log.Information($"请求错误的箱子ID[{_inventoryID}]来自[{inventory.GetType().Name}][{extra}]");
                    });
                }

                break;

            case EventType.HandleMoveItem:
                if (_sourceInventorySlot != null)
                {
                    sourceInventoryObject = subsystemInventories?.GetInventoryById(_sourceInventorySlot.InventoryId);
                    if (_targetInventorySlot != null)
                    {
                        targetInventoryObject =
                            subsystemInventories?.GetInventoryById(_targetInventorySlot.InventoryId);
                        if (sourceInventoryObject is not null)
                            // 数据捕捉
                        {
                            if (targetInventoryObject != null)
                            {
                                InventorySlotWidget.HandleMoveItem(sourceInventoryObject,
                                    _sourceInventorySlot.SlotIndex,
                                    targetInventoryObject, _targetInventorySlot.SlotIndex, _targetInventorySlot.Count);
                            }
                        }
                    }
                }

                // 服务器找不到背包？怀疑是来打服的！！！
                break;
            case EventType.HandleDragDrop:
                if (_sourceInventorySlot != null)
                {
                    sourceInventoryObject = subsystemInventories?.GetInventoryById(_sourceInventorySlot.InventoryId);
                    if (_targetInventorySlot != null)
                    {
                        targetInventoryObject =
                            subsystemInventories?.GetInventoryById(_targetInventorySlot.InventoryId);
                        if (sourceInventoryObject is not null)
                            // 数据捕捉
                        {
                            if (targetInventoryObject != null)
                            {
                                InventorySlotWidget.HandleDragDrop(sourceInventoryObject,
                                    _sourceInventorySlot.SlotIndex,
                                    DragMode, targetInventoryObject, _targetInventorySlot.SlotIndex, ProcessingOnly);
                            }
                        }
                    }
                }

                break;
            case EventType.SetSlotsItem:
                if (isServer)
                {
                }
                else
                {
                    if (_sourceInventorySlot != null)
                    {
                        sourceInventoryObject =
                            subsystemInventories?.GetInventoryById(_sourceInventorySlot.InventoryId);
                        if (sourceInventoryObject is ComponentInventoryBase componentInventoryBase)
                        {
                            var slot = new ComponentInventoryBase.Slot
                            {
                                Value = _sourceInventorySlot.Value,
                                Count = _sourceInventorySlot.Count
                            };
                            componentInventoryBase.SetSlotValue(_sourceInventorySlot.SlotIndex, slot);
                        }
                    }
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
