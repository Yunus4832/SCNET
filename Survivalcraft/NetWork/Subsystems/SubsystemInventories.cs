using EntitySystem.Core;
using Game.NetWork;
using Game.NetWork.Packages;

namespace Game.Subsystems;

public class SubsystemInventories : Subsystem
{
    private static readonly Dictionary<IInventory, List<int>> _syncItems = new();

    private readonly Dictionary<int, IInventory> _inventories = new();


    public int ProduceInventoryId(IInventory inventory)
    {
        for (var i = 1; i < int.MaxValue; i++)
        {
            if (_inventories.TryAdd(i, inventory))
            {
                return i;
            }
        }

        throw new Exception("新增Inventory失败");
    }

    public int RegisterInventory(IInventory inventory)
    {
        if (_inventories.TryAdd(inventory.Id, inventory))
        {
            return inventory.Id;
        }

        if (CommonLib.WorkType == WorkType.Client)
        {
            _inventories[inventory.Id] = inventory;
            return inventory.Id;
        }

        var id = ProduceInventoryId(inventory);
        return id;
    }

    public bool FindInventoryById(int id, Action<IInventory>? action = null)
    {
        if (_inventories.TryGetValue(id, out var inventory))
        {
            action?.Invoke(inventory);
            return true;
        }
#if DEBUG
        CommonLib.Net.QueuePackage(new ComponentInventoryPackage(id,
            ComponentInventoryPackage.EventType.QueryErrorInventoryInfo));
#endif
        return false;
    }

    public IInventory? GetInventoryById(int id)
    {
        return _inventories.TryGetValue(id, out var inventory) ? inventory : null;
    }

    public static void PushSyncItem(IInventory inventory, int slotIndex)
    {
        if (!_syncItems.TryGetValue(inventory, out var list))
        {
            list = [];
            _syncItems.Add(inventory, list);
        }

        if (!list.Contains(slotIndex))
        {
            list.Add(slotIndex);
        }
    }

    public static void FlushSyncItems()
    {
        if (_syncItems.Count <= 0)
        {
            return;
        }

        if (CommonLib.WorkType == WorkType.Server)
        {
            CommonLib.Net.QueuePackage(new ComponentInventoryPackage(_syncItems));
        }

        _syncItems.Clear();
    }

    public override void OnEntityRemoved(Entity entity)
    {
        foreach (var i in entity.FindComponents<IInventory>().OfType<IInventory>())
        {
            _inventories.Remove(i.Id);
        }
    }
}
