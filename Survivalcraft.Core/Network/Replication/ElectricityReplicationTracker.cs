namespace Game.Network.Replication;

public sealed class ElectricityReplicationTracker
{
    private readonly Dictionary<WorldConnection, Dictionary<Point3, float>> _states =
        new(ReferenceEqualityComparer.Instance);

    public ElectricityVoltageChanges Capture(WorldConnection client, IReadOnlyDictionary<Point3, float> current)
    {
        var baseline = !_states.TryGetValue(client, out var previous);
        var changed = new Dictionary<Point3, float>();
        foreach (var (point, voltage) in current)
        {
            if (baseline || !previous!.TryGetValue(point, out var oldVoltage) || !oldVoltage.Equals(voltage))
            {
                changed[point] = voltage;
            }
        }

        var removed = baseline ? [] : previous!.Keys.Where(point => !current.ContainsKey(point)).ToArray();
        _states[client] = new Dictionary<Point3, float>(current);
        return new ElectricityVoltageChanges(baseline, changed, removed);
    }

    public void RetainClients(IEnumerable<WorldConnection> clients)
    {
        var active = new HashSet<WorldConnection>(clients, ReferenceEqualityComparer.Instance);
        foreach (var client in _states.Keys.Where(client => !active.Contains(client)).ToArray())
        {
            _states.Remove(client);
        }
    }
}
