using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Network;
using Game.Network.Enums;

namespace Game.Subsystems;

public class SubsystemItemsScanner : Subsystem, IUpdateable
{
    public const float AutomaticScanPeriod = 60f;

    private readonly List<ScannedItemData> _items = [];

    private double _nextAutomaticScanTime;

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        if (CommonLib.WorkType == WorkType.Client)
        {
            return;
        }

        if (!(Time.FrameStartTime >= _nextAutomaticScanTime))
        {
            return;
        }

        _nextAutomaticScanTime = Time.FrameStartTime + AutomaticScanPeriod;
        ItemsScanned?.Invoke(ScanItems());
    }

    public event Action<ReadOnlyList<ScannedItemData>>? ItemsScanned;

    public ReadOnlyList<ScannedItemData> ScanItems()
    {
        _items.Clear();
        foreach (var subsystem in Project.ReadOnlySubsystems)
        {
            var inventory = subsystem as IInventory;
            if (inventory != null)
            {
                ScanInventory(inventory, _items);
            }
        }

        foreach (var entity in Project.EntityKeys)
        {
            foreach (var component in entity.Components)
            {
                var inventory2 = component as IInventory;
                if (inventory2 != null)
                {
                    ScanInventory(inventory2, _items);
                }
            }
        }

        ScannedItemData item;
        foreach (var pickable in Project.FindSubsystem<SubsystemPickables>(true)!.Pickables)
        {
            if (pickable.Count > 0 && pickable.Value != 0)
            {
                item = new ScannedItemData
                {
                    Container = pickable,
                    Value = pickable.Value,
                    Count = pickable.Count
                };
                _items.Add(item);
            }
        }

        foreach (var projectile in Project.FindSubsystem<SubsystemProjectiles>(true)!.Projectiles)
        {
            if (projectile.Value != 0)
            {
                item = new ScannedItemData
                {
                    Container = projectile,
                    Value = projectile.Value,
                    Count = 1
                };
                _items.Add(item);
            }
        }

        foreach (var movingBlockSet in Project.FindSubsystem<SubsystemMovingBlocks>(true)!.ReadonlyMovingBlockSets)
        {
            for (var i = 0; i < movingBlockSet.Blocks.Count; i++)
            {
                item = new ScannedItemData
                {
                    Container = movingBlockSet,
                    Value = movingBlockSet.Blocks[i].Value,
                    Count = 1,
                    IndexInContainer = i
                };
                _items.Add(item);
            }
        }

        return new ReadOnlyList<ScannedItemData>(_items);
    }

    public bool TryModifyItem(ScannedItemData itemData, int newValue)
    {
        if (itemData.Container is IInventory obj)
        {
            obj.RemoveSlotItems(itemData.IndexInContainer, itemData.Count);
            var slotCapacity = obj.GetSlotCapacity(itemData.IndexInContainer, newValue);
            obj.AddSlotItems(itemData.IndexInContainer, newValue, MathUtils.Min(itemData.Count, slotCapacity));
            return true;
        }

        if (itemData.Container is WorldItem item)
        {
            item.Value = newValue;
            return true;
        }

        if (itemData.Container is not IMovingBlockSet obj2)
        {
            return false;
        }

        var movingBlock = obj2.Blocks.ElementAt(itemData.IndexInContainer);
        obj2.SetBlock(movingBlock.Offset, newValue);
        return true;
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        _nextAutomaticScanTime = Time.FrameStartTime + AutomaticScanPeriod;
    }

    private void ScanInventory(IInventory inventory, List<ScannedItemData> items)
    {
        for (var i = 0; i < inventory.SlotsCount; i++)
        {
            var slotCount = inventory.GetSlotCount(i);
            if (slotCount <= 0)
            {
                continue;
            }

            var slotValue = inventory.GetSlotValue(i);
            if (slotValue != 0)
            {
                items.Add(new ScannedItemData
                {
                    Container = inventory,
                    IndexInContainer = i,
                    Value = slotValue,
                    Count = slotCount
                });
            }
        }
    }
}
