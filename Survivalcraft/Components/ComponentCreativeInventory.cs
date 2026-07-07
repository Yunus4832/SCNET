using System.Globalization;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public class ComponentCreativeInventory : Component, IInventory
{
    public const int LargeNumber = 9999;

    private int _activeSlotIndex;

    private readonly List<int> _slots = [];

    public int OpenSlotsCount { get; set; }

    public int CategoryIndex { get; set; }

    public int PageIndex { get; set; }

    public int Id { get; private set; }

    public int ActiveSlotIndex
    {
        get => _activeSlotIndex;
        set => _activeSlotIndex = MathUtils.Clamp(value, 0, VisibleSlotsCount - 1);
    }

    public int SlotsCount => _slots.Count;

    public void DropAllItems(Vector3 position)
    {
    }

    public int VisibleSlotsCount
    {
        get;
        set
        {
            value = MathUtils.Clamp(value, 0, 10);
            if (value == field)
            {
                return;
            }

            field = value;
            ActiveSlotIndex = ActiveSlotIndex;
            var componentFrame = Entity.FindComponent<ComponentFrame>();
            if (componentFrame == null)
            {
                return;
            }

            var position = componentFrame.Position + new Vector3(0f, 0.5f, 0f);
            var velocity = 1f * componentFrame.Rotation.GetForwardVector();
            for (var i = field; i < 10; i++)
            {
                DropSlotItems(i, position, velocity);
            }
        }
    } = 10;

    public virtual void SetSlotValue(int slotIndex, object obj)
    {
        var slot = (ComponentInventoryBase.Slot)obj;
        _slots[slotIndex] = slot.Value;
    }

    public virtual void OnSlotChange(int slotIndex)
    {
        SubsystemInventories.PushSyncItem(this, slotIndex);
    }

    public int GetSlotValue(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < _slots.Count)
        {
            return _slots[slotIndex];
        }

        return 0;
    }

    public int GetSlotCount(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _slots.Count)
        {
            return 0;
        }

        return _slots[slotIndex] == 0 ? 0 : 9999;
    }

    public int GetSlotCapacity(int slotIndex, int value)
    {
        if (slotIndex >= VisibleSlotsCount && slotIndex < 10)
        {
            return 0;
        }

        if (slotIndex >= 0 && slotIndex < OpenSlotsCount)
        {
            return 99980001;
        }

        return GetSlotCount(slotIndex);
    }

    public int GetSlotProcessCapacity(int slotIndex, int value)
    {
        var slotCount = GetSlotCount(slotIndex);
        var slotValue = GetSlotValue(slotIndex);
        if (slotCount <= 0 || slotValue == 0)
        {
            return slotIndex < OpenSlotsCount ? 0 : 9999;
        }

        var blockBehaviors = Project.FindSubsystem<SubsystemBlockBehaviors>(true)!
            .GetBlockBehaviors(Terrain.ExtractContents(slotValue));
        foreach (var blockBehavior in blockBehaviors)
        {
            var processInventoryItemCapacity =
                blockBehavior.GetProcessInventoryItemCapacity(this, slotIndex, value);
            if (processInventoryItemCapacity > 0)
            {
                return processInventoryItemCapacity;
            }
        }

        return slotIndex < OpenSlotsCount ? 0 : 9999;
    }

    public void AddSlotItems(int slotIndex, int value, int count)
    {
        AddNetSlotItems(slotIndex, value, count);
        OnSlotChange(slotIndex);
    }

    public bool AddNetSlotItems(int slotIndex, int value, int count)
    {
        if (slotIndex < 0 || slotIndex >= OpenSlotsCount)
        {
            return false;
        }

        if (_slots[slotIndex] == value)
        {
            return false;
        }

        _slots[slotIndex] = value;
        return true;

    }

    public int RemoveSlotItems(int slotIndex, int count)
    {
        count = RemoveNetSlotItems(slotIndex, count);
        OnSlotChange(slotIndex);
        return count;
    }

    public int RemoveNetSlotItems(int slotIndex, int count)
    {
        if (slotIndex < 0 || slotIndex >= OpenSlotsCount)
        {
            return 1;
        }

        var num = Terrain.ExtractContents(_slots[slotIndex]);
        if (BlocksManager.Blocks[num].NonDuplicable)
        {
            _slots[slotIndex] = 0;
        }

        if (count >= 9999)
        {
            _slots[slotIndex] = 0;
        }

        return 1;
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

        if (slotIndex >= OpenSlotsCount)
        {
            processedValue = 0;
            processedCount = 0;
        }
        else
        {
            processedValue = value;
            processedCount = count;
        }
    }

    public void DropSlotItems(int slotIndex, Vector3 position, Vector3 velocity)
    {
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        _activeSlotIndex = valuesDictionary.GetValue<int>("ActiveSlotIndex");
        OpenSlotsCount = valuesDictionary.GetValue<int>("OpenSlotsCount");
        CategoryIndex = valuesDictionary.GetValue<int>("CategoryIndex");
        PageIndex = valuesDictionary.GetValue<int>("PageIndex");
        Id = valuesDictionary.GetValue("Id", -1);
        var subInventory = Project.FindSubsystem<SubsystemInventories>(true)!;
        Id = Id == -1 ? subInventory.ProduceInventoryId(this) : subInventory.RegisterInventory(this);
        for (var i = 0; i < OpenSlotsCount; i++)
        {
            _slots.Add(0);
        }

        if (Project.FindSubsystem<SubsystemGameInfo>(true)!.WorldSettings.GameMode != GameMode.Creative)
        {
            return;
        }

        var creativeValues = BlocksManager.GetCreativeValues().ToArray();
        foreach (var creativeValue in creativeValues)
        {
            _slots.Add(creativeValue);
        }

        var externalValues = creativeValues
            .Where(creativeValue =>
                BlocksManager.TryGetBlockId(Terrain.ExtractContents(creativeValue), out var id) &&
                id.Namespace != new ModId("game"))
            .ToArray();
        Log.Information(
            "Initialized creative inventory with {0} registered values; external values: {1}.",
            creativeValues.Length,
            string.Join(",", externalValues));

        var value = valuesDictionary.GetValue<ValuesDictionary>("Slots", false);
        if (value == null)
        {
            return;
        }

        for (var j = 0; j < OpenSlotsCount; j++)
        {
            var value2 = value.GetValue<ValuesDictionary>("Slot" + j.ToString(CultureInfo.InvariantCulture), false);
            if (value2 != null)
            {
                _slots[j] = value2.GetValue<int>("Contents");
            }
        }
    }

    public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
    {
        valuesDictionary.SetValue("ActiveSlotIndex", _activeSlotIndex);
        valuesDictionary.SetValue("CategoryIndex", CategoryIndex);
        valuesDictionary.SetValue("PageIndex", PageIndex);
        valuesDictionary.SetValue("Id", Id);
        var valuesDictionary2 = new ValuesDictionary();
        valuesDictionary.SetValue("Slots", valuesDictionary2);
        for (var i = 0; i < OpenSlotsCount; i++)
        {
            if (_slots[i] != 0)
            {
                var valuesDictionary3 = new ValuesDictionary();
                valuesDictionary2.SetValue("Slot" + i.ToString(CultureInfo.InvariantCulture), valuesDictionary3);
                valuesDictionary3.SetValue("Contents", _slots[i]);
            }
        }
    }

    public void DropNetSlotItems(int slotIndex, Vector3 position, Vector3 velocity)
    {
    }

    public void DropNetAllItems(Vector3 position)
    {
    }
}
