namespace Game.Network.Replication;

public sealed class ChunkInterestTracker
{
    private readonly Dictionary<WorldConnection, HashSet<Point2>> _chunks = new(ReferenceEqualityComparer.Instance);

    public ChunkInterestChanges Synchronize(WorldConnection client, HashSet<Point2> current)
    {
        var hasPrevious = _chunks.TryGetValue(client, out var previous);
        var entered = hasPrevious
            ? current.Where(chunk => !previous!.Contains(chunk)).ToArray()
            : current.ToArray();
        var left = hasPrevious ? previous!.Where(chunk => !current.Contains(chunk)).ToArray() : [];
        if (hasPrevious)
        {
            previous!.ExceptWith(left);
            previous.UnionWith(entered);
        }
        else
        {
            _chunks.Add(client, [.. current]);
        }

        return new ChunkInterestChanges(entered, left);
    }

    public void RetainClients(IEnumerable<WorldConnection> clients)
    {
        var retained = new HashSet<WorldConnection>(clients, ReferenceEqualityComparer.Instance);
        foreach (var client in _chunks.Keys.Where(client => !retained.Contains(client)).ToArray())
        {
            _chunks.Remove(client);
        }
    }
}
