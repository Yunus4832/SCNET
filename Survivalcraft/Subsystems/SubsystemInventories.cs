using EntitySystem.Core;

using Game.Network;
using Game.Network.Enums;
using Game.Network.Packages;

namespace Game.Subsystems;

public class SubsystemInventories : Subsystem, IUpdateable
{
    private static readonly Dictionary<IInventory, List<int>> _syncItems = new();

    private readonly Dictionary<int, IInventory> _inventories = new();

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        if (CommonLib.WorkType != WorkType.Server || !Time.PeriodicEvent(0.1, 0))
        {
            return;
        }

        var interest = Project.FindSubsystem<SubsystemNetworkInterest>(true)!;
        var relevant = new Dictionary<Client, HashSet<int>>(ReferenceEqualityComparer.Instance);
        foreach (var inventory in _inventories.Values)
        {
            foreach (var client in GetObservers(inventory).Where(client => client.IsConnected))
            {
                if (!relevant.TryGetValue(client, out var ids))
                {
                    ids = [];
                    relevant.Add(client, ids);
                }

                ids.Add(inventory.Id);
            }
        }

        foreach (var client in CommonLib.Net.Clients.Values.Where(client =>
                     client.IsConnected && client.State == ClientState.Playing && client != CommonLib.Net.Self))
        {
            var current = relevant.TryGetValue(client, out var ids) ? ids : [];
            var changes = interest.Entities.Synchronize(
                client, EntityInterestGroup.Inventories, current, _inventories.ContainsKey, 1);
            if (changes.Entered.Count == 0)
            {
                continue;
            }

            var baseline = new Dictionary<IInventory, List<int>>();
            foreach (var id in changes.Entered)
            {
                var inventory = _inventories[id];
                baseline.Add(inventory, Enumerable.Range(0, inventory.SlotsCount).ToList());
            }

            NetworkSender.SendTo(client, new ComponentInventoryPackage(baseline));
            foreach (var inventory in baseline.Keys.Where(inventory => inventory.ActiveSlotIndex >= 0))
            {
                NetworkSender.SendTo(client,
                    new ComponentInventoryPackage(inventory, inventory.ActiveSlotIndex));
            }
        }
    }

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

        if (CommonLib.WorkType == WorkType.Client && Log.MinimumLogType is LogType.Debug)
        {
            NetworkSender.SendToServer(new ComponentInventoryPackage(id,
                ComponentInventoryPackage.EventType.QueryErrorInventoryInfo));
        }

        return false;
    }

    public IInventory? GetInventoryById(int id)
    {
        return _inventories.TryGetValue(id, out var inventory) ? inventory : null;
    }

    public static IEnumerable<Client> GetObservers(IInventory inventory)
    {
        if (inventory is not Component component)
        {
            return [];
        }

        var interest = component.Project.FindSubsystem<SubsystemNetworkInterest>(true)!;
        if (component.Entity.FindComponent<ComponentBlockEntity>() is not null)
        {
            return interest.Entities.GetObservers(EntityInterestGroup.BlockEntities, component.Entity.EntityId);
        }

        if (component.Entity.FindComponent<ComponentBody>() is not { } body)
        {
            return [];
        }

        var observers = interest.Entities.GetObservers(EntityInterestGroup.Creatures, component.Entity.EntityId);
        return body.Player?.PlayerData.Client is { IsConnected: true } owner
            ? observers.Append(owner).Distinct<Client>(ReferenceEqualityComparer.Instance)
            : observers;
    }

    public static bool CanClientAccess(IInventory inventory, Client client)
    {
        if (inventory is not Component component)
        {
            return false;
        }

        if (component.Entity.FindComponent<ComponentBlockEntity>() is { } block)
        {
            return component.Project.FindSubsystem<SubsystemNetworkInterest>(true)!
                .IsPositionRelevant(client, new Vector2(block.Coordinates.X, block.Coordinates.Z));
        }

        return component.Entity.FindComponent<ComponentPlayer>() is { } player &&
               ReferenceEquals(player.PlayerData.Client, client);
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
            var batches = new Dictionary<Client, Dictionary<IInventory, List<int>>>(
                ReferenceEqualityComparer.Instance);
            foreach (var (inventory, slots) in _syncItems)
            {
                foreach (var client in GetObservers(inventory).Where(client =>
                             client.IsConnected && client.State == ClientState.Playing))
                {
                    if (!batches.TryGetValue(client, out var batch))
                    {
                        batch = [];
                        batches.Add(client, batch);
                    }

                    batch[inventory] = slots;
                }
            }

            foreach (var (client, batch) in batches)
            {
                NetworkSender.SendTo(client, new ComponentInventoryPackage(batch));
            }
        }

        _syncItems.Clear();
    }

    public override void OnEntityRemoved(Entity entity)
    {
        foreach (var i in entity.FindComponents<IInventory>().OfType<IInventory>())
        {
            _inventories.Remove(i.Id);
            _syncItems.Remove(i);
            Project.FindSubsystem<SubsystemNetworkInterest>(true)!
                .Entities.RemoveEntity(EntityInterestGroup.Inventories, i.Id);
        }
    }
}
