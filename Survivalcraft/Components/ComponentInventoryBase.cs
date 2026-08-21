using System.Globalization;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Network;
using Game.Network.Enums;

namespace Game.Components;

public abstract class ComponentInventoryBase : Component, IInventory
{
    protected readonly Random sharedRandom = new();

    protected readonly List<Slot> slots = [];

    Project IInventory.Project => Project;

    public int Id { get; private set; }

    public virtual int SlotsCount => slots.Count;

    public virtual int VisibleSlotsCount
    {
        get => SlotsCount;
        set { }
    }

    public virtual int ActiveSlotIndex
    {
        get => -1;
        set { }
    }

    public virtual int GetSlotValue(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= slots.Count)
        {
            return 0;
        }

        return slots[slotIndex].Count <= 0 ? 0 : slots[slotIndex].Value;
    }

    public virtual int GetSlotCount(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < slots.Count)
        {
            return slots[slotIndex].Count;
        }

        return 0;
    }

    public virtual int GetSlotCapacity(int slotIndex, int value)
    {
        if (slotIndex >= 0 && slotIndex < slots.Count)
        {
            return BlocksManager.Blocks[Terrain.ExtractContents(value)].GetMaxStacking(value);
        }

        return 0;
    }

    public virtual int GetSlotProcessCapacity(int slotIndex, int value)
    {
        var slotCount = GetSlotCount(slotIndex);
        var slotValue = GetSlotValue(slotIndex);
        if (slotCount <= 0 || slotValue == 0)
        {
            return 0;
        }

        var blockBehaviors = Project.FindSubsystem<SubsystemBlockBehaviors>(true)!
            .GetBlockBehaviors(Terrain.ExtractContents(slotValue));
        return blockBehaviors
            .Select(blockBehavior => blockBehavior.GetProcessInventoryItemCapacity(this, slotIndex, value))
            .FirstOrDefault(processInventoryItemCapacity => processInventoryItemCapacity > 0);
    }

    public virtual void ProcessSlotItems(IInventory sourceInventory, int sourceSlotIndex, int slotIndex, int value,
        int count, int processCount, out int processedValue, out int processedCount)
    {
        var slotCount = GetSlotCount(slotIndex);
        var slotValue = GetSlotValue(slotIndex);
        if (slotCount > 0 && slotValue != 0)
        {
            var blockBehaviors = Project.FindSubsystem<SubsystemBlockBehaviors>(true)!
                .GetBlockBehaviors(Terrain.ExtractContents(slotValue));
            foreach (var subsystemBlockBehavior in blockBehaviors)
            {
                var processInventoryItemCapacity =
                    subsystemBlockBehavior.GetProcessInventoryItemCapacity(this, slotIndex, value);
                if (processInventoryItemCapacity <= 0)
                {
                    continue;
                }

                subsystemBlockBehavior.ProcessInventoryItem(this, slotIndex, value, count,
                    MathUtils.Min(processInventoryItemCapacity, processCount), out processedValue,
                    out processedCount);
                return;
            }
        }

        processedValue = value;
        processedCount = count;
    }

    public virtual void SetSlotValue(int slotIndex, object obj)
    {
        var slot = (Slot)obj;
        slots[slotIndex].Count = slot.Count;
        slots[slotIndex].Value = slot.Value;
        OnSlotChange(slotIndex);
    }

    public virtual void AddSlotItems(int slotIndex, int value, int count)
    {
        AddNetSlotItems(slotIndex, value, count);
        OnSlotChange(slotIndex);
    }

    public virtual int RemoveSlotItems(int slotIndex, int count)
    {
        count = RemoveNetSlotItems(slotIndex, count);
        OnSlotChange(slotIndex);
        return count;
    }

    public virtual void OnSlotChange(int slotIndex)
    {
        // 只有服务器能够发同步，禁止客户端的数据来同步服务器
        if (CommonLib.WorkType == WorkType.Server)
        {
            SubsystemInventories.PushSyncItem(this, slotIndex);
        }
    }

    public virtual bool AddNetSlotItems(int slotIndex, int value, int count)
    {
        if (count > 0 && slotIndex >= 0 && slotIndex < slots.Count)
        {
            var slot = slots[slotIndex];
            if ((GetSlotCount(slotIndex) != 0 && !BlocksManager.Blocks[Terrain.ExtractContents(value)]
                    .CanAutoStack(GetSlotValue(slotIndex), value)) ||
                GetSlotCount(slotIndex) + count > GetSlotCapacity(slotIndex, value))
            {
                return false;
            }

            slot.Value = value;
            slot.Count += count;
            return true;
        }

        return false;
    }

    public virtual int RemoveNetSlotItems(int slotIndex, int count)
    {
        if (slotIndex >= 0 && slotIndex < slots.Count)
        {
            var slot = slots[slotIndex];
            count = MathUtils.Min(count, GetSlotCount(slotIndex));
            slot.Count -= count;
            if (slot.Count == 0)
            {
                slot.Value = 0;
            }

            return count;
        }

        return 0;
    }

    public virtual void DropAllItems(Vector3 position)
    {
        // 这是背包破坏后的物品调出，例如拆掉箱子
        for (var i = 0; i < SlotsCount; i++)
        {
            DropSlotItems(i, position,
                sharedRandom.Float(5f, 10f) * Vector3.Normalize(new Vector3(sharedRandom.Float(-1f, 1f),
                    sharedRandom.Float(1f, 2f),
                    sharedRandom.Float(-1f, 1f))));
        }
    }

    public virtual void DropSlotItems(int slotIndex, Vector3 position, Vector3 velocity)
    {
        var slotCount = GetSlotCount(slotIndex);
        if (slotCount <= 0)
        {
            return;
        }

        var slotValue = GetSlotValue(slotIndex);
        var num = RemoveSlotItems(slotIndex, slotCount);
        if (num > 0)
        {
            Project.FindSubsystem<SubsystemPickables>(true)!.AddPickable(slotValue, num, position, velocity, null);
        }
    }

    public static int FindAcquireSlotForItem(IInventory inventory, int value)
    {
        for (var i = 0; i < inventory.SlotsCount; i++)
        {
            if (inventory.GetSlotCount(i) > 0 && BlocksManager.Blocks[Terrain.ExtractContents(value)]
                    .CanAutoStack(inventory.GetSlotValue(i), value) &&
                inventory.GetSlotCount(i) < inventory.GetSlotCapacity(i, value))
            {
                return i;
            }
        }

        for (var j = 0; j < inventory.SlotsCount; j++)
        {
            if (inventory.GetSlotCount(j) == 0 && inventory.GetSlotCapacity(j, value) > 0)
            {
                return j;
            }
        }

        return -1;
    }

    public static int AcquireItems(IInventory inventory, int value, int count)
    {
        if (CommonLib.WorkType != WorkType.Client)
        {
            while (count > 0)
            {
                var num = FindAcquireSlotForItem(inventory, value);
                if (num < 0)
                {
                    break;
                }

                inventory.AddSlotItems(num, value, 1);
                count--;
            }

            return count;
        }

        return 0;
    }

    protected ComponentPlayer? FindInteractingPlayer()
    {
        var componentPlayer = Entity.FindComponent<ComponentPlayer>();
        if (componentPlayer != null)
        {
            return componentPlayer;
        }

        var componentBlockEntity = Entity.FindComponent<ComponentBlockEntity>();
        if (componentBlockEntity == null)
        {
            return componentPlayer;
        }

        var position = new Vector3(componentBlockEntity.Coordinates);
        componentPlayer = Project.FindSubsystem<SubsystemPlayers>(true)!.FindNearestPlayer(position);

        return componentPlayer;
    }


    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        Id = valuesDictionary.GetValue("Id", -1);
        var subInventory = Project.FindSubsystem<SubsystemInventories>(true)!;
        Id = Id == -1 ? subInventory.ProduceInventoryId(this) : subInventory.RegisterInventory(this);
        var value = valuesDictionary.GetValue<int>("SlotsCount");
        for (var i = 0; i < value; i++)
        {
            slots.Add(new Slot());
        }

        var value2 = valuesDictionary.GetValue<ValuesDictionary>("Slots");
        for (var j = 0; j < slots.Count; j++)
        {
            var value3 = value2.GetValue<ValuesDictionary>("Slot" + j.ToString(CultureInfo.InvariantCulture), false);
            if (value3 == null)
            {
                continue;
            }

            var slot = slots[j];
            slot.Value = value3.GetValue<int>("Contents");
            slot.Count = value3.GetValue<int>("Count");
        }
    }

    public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
    {
        var valuesDictionary2 = new ValuesDictionary();
        valuesDictionary.SetValue("Slots", valuesDictionary2);
        valuesDictionary.SetValue("Id", Id);
        for (var i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            if (slot.Count <= 0)
            {
                continue;
            }

            var valuesDictionary3 = new ValuesDictionary();
            valuesDictionary2.SetValue("Slot" + i.ToString(CultureInfo.InvariantCulture), valuesDictionary3);
            valuesDictionary3.SetValue("Contents", slot.Value);
            valuesDictionary3.SetValue("Count", slot.Count);
        }
    }

    public sealed class Slot
    {
        public int Value { get; set; }

        public int Count { get; set; }
    }
}
