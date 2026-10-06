namespace Game.Network;

public enum EntityInterestGroup
{
    Creatures,
    Pickables,
    Projectiles,
    MovingBlocks,
    Inventories,
    BlockEntities
}

public readonly record struct EntityInterestChanges(
    IReadOnlyList<int> Entered,
    IReadOnlyList<int> Left);

public sealed class EntityInterestTracker
{
    private readonly Dictionary<Client, Dictionary<EntityInterestGroup, EntitySetState>> _states =
        new(ReferenceEqualityComparer.Instance);

    public void Seed(Client client, EntityInterestGroup group, IEnumerable<int> entityIds)
    {
        var state = GetOrCreateState(client, group);
        state.Observed.Clear();
        state.OutOfRangeTicks.Clear();
        state.Observed.UnionWith(entityIds);
    }

    public EntityInterestChanges Synchronize(
        Client client,
        EntityInterestGroup group,
        IReadOnlySet<int> current,
        Func<int, bool> isTrackable,
        int leaveDelayTicks)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(leaveDelayTicks, 1);
        var state = GetOrCreateState(client, group);
        var entered = new List<int>();
        var left = new List<int>();

        foreach (var entityId in state.Observed.ToArray())
        {
            if (current.Contains(entityId))
            {
                state.OutOfRangeTicks.Remove(entityId);
                continue;
            }

            if (!isTrackable(entityId))
            {
                state.Observed.Remove(entityId);
                state.OutOfRangeTicks.Remove(entityId);
                continue;
            }

            var ticks = state.OutOfRangeTicks.TryGetValue(entityId, out var count) ? count + 1 : 1;
            if (ticks < leaveDelayTicks)
            {
                state.OutOfRangeTicks[entityId] = ticks;
                continue;
            }

            state.Observed.Remove(entityId);
            state.OutOfRangeTicks.Remove(entityId);
            left.Add(entityId);
        }

        foreach (var entityId in current)
        {
            if (!state.Observed.Add(entityId))
            {
                continue;
            }

            state.OutOfRangeTicks.Remove(entityId);
            entered.Add(entityId);
        }

        return new EntityInterestChanges(entered, left);
    }

    public void RemoveClient(Client client)
    {
        _states.Remove(client);
    }

    public void RetainClients(IEnumerable<Client> clients)
    {
        var active = new HashSet<Client>(clients, ReferenceEqualityComparer.Instance);
        foreach (var client in _states.Keys.ToArray())
        {
            if (!active.Contains(client))
            {
                _states.Remove(client);
            }
        }
    }

    public void AddObserved(Client client, EntityInterestGroup group, int entityId)
    {
        var state = GetOrCreateState(client, group);
        state.Observed.Add(entityId);
        state.OutOfRangeTicks.Remove(entityId);
    }

    public IEnumerable<Client> GetObservers(EntityInterestGroup group, int entityId)
    {
        foreach (var (client, groups) in _states)
        {
            if (groups.TryGetValue(group, out var state) && state.Observed.Contains(entityId))
            {
                yield return client;
            }
        }
    }

    public void RemoveEntity(EntityInterestGroup group, int entityId)
    {
        foreach (var groups in _states.Values)
        {
            if (groups.TryGetValue(group, out var state))
            {
                state.Observed.Remove(entityId);
                state.OutOfRangeTicks.Remove(entityId);
            }
        }
    }

    private EntitySetState GetOrCreateState(Client client, EntityInterestGroup group)
    {
        if (!_states.TryGetValue(client, out var groups))
        {
            groups = [];
            _states.Add(client, groups);
        }

        if (!groups.TryGetValue(group, out var state))
        {
            state = new EntitySetState();
            groups.Add(group, state);
        }

        return state;
    }

    private sealed class EntitySetState
    {
        public readonly HashSet<int> Observed = [];

        public readonly Dictionary<int, int> OutOfRangeTicks = [];
    }
}
