using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public class ComponentFurnitureInventory : Component, IInventory
{
    public const int LargeNumber = 9999;

    public const int MaxDesign = 65535;

    private readonly List<int> _slots = [];

    private SubsystemFurnitureBlockBehavior _subsystemFurnitureBlockBehavior = null!;

    public int PageIndex { get; set; }

    public FurnitureSet FurnitureSet { get; set; } = null!;

    public int Id { get; set; }

    Project IInventory.Project => Project;

    public int ActiveSlotIndex
    {
        get => -1;
        set { }
    }

    public int SlotsCount => _slots.Count;

    public int VisibleSlotsCount
    {
        get => SlotsCount;
        set { }
    }

    public virtual void OnSlotChange(int slotIndex)
    {
        SubsystemInventories.PushSyncItem(this, slotIndex);
    }

    public virtual void SetSlotValue(int slotIndex, object obj)
    {
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
        return 99980001;
    }

    public int GetSlotProcessCapacity(int slotIndex, int value)
    {
        var slotCount = GetSlotCount(slotIndex);
        var slotValue = GetSlotValue(slotIndex);
        if (slotCount <= 0 || slotValue == 0)
        {
            return 9999;
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

        return 9999;
    }

    public void AddSlotItems(int slotIndex, int value, int count)
    {
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

        processedValue = 0;
        processedCount = 0;
    }

    public int RemoveSlotItems(int slotIndex, int count)
    {
        return 1;
    }

    public void DropAllItems(Vector3 position)
    {
    }

    public bool AddNetSlotItems(int slotIndex, int value, int count)
    {
        return false;
    }

    public int RemoveNetSlotItems(int slotIndex, int count)
    {
        return 0;
    }

    public void DropSlotItems(int slotIndex, Vector3 position, Vector3 velocity)
    {
    }

    public void FillSlots()
    {
        _subsystemFurnitureBlockBehavior.GarbageCollectDesigns();
        _slots.Clear();
        for (var i = 0; i < MaxDesign; i++)
        {
            var design = _subsystemFurnitureBlockBehavior.GetDesign(i);
            if (design == null)
            {
                continue;
            }

            var num = (from f in design.ListChain()
                select f.Index).Min();
            if (design.Index != num)
            {
                continue;
            }

            var data = FurnitureBlock.SetDesignIndex(0, i, design.ShadowStrengthFactor, design.IsLightEmitter);
            var item = Terrain.MakeBlockValue(FurnitureBlock.Index, 0, data);
            _slots.Add(item);
        }
    }

    public void ClearSlots()
    {
        _slots.Clear();
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        Id = valuesDictionary.GetValue("Id", -1);
        var subInventory = Project.FindSubsystem<SubsystemInventories>(true)!;
        Id = Id == -1 ? subInventory.ProduceInventoryId(this) : subInventory.RegisterInventory(this);
        _subsystemFurnitureBlockBehavior = Project.FindSubsystem<SubsystemFurnitureBlockBehavior>(true)!;
        var furnitureSetName = valuesDictionary.GetValue<string>("FurnitureSet");
        FurnitureSet = _subsystemFurnitureBlockBehavior.FurnitureSets.FirstOrDefault(
            f => f.Name == furnitureSetName,
            FurnitureSetDefault.Default
        );
    }

    public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
    {
        valuesDictionary.SetValue("FurnitureSet", FurnitureSet.Name);
        valuesDictionary.SetValue("Id", Id);
    }

    public void DropNetSlotItems(int slotIndex, Vector3 position, Vector3 velocity)
    {
    }

    public void DropNetAllItems(Vector3 position)
    {
    }
}
