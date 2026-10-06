namespace Game.Network;

public sealed class ChunkInterestTracker
{
    private readonly Dictionary<Client, HashSet<Point2>> _chunks = new(ReferenceEqualityComparer.Instance);

    public ChunkInterestChanges Synchronize(Client client, HashSet<Point2> current)
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
            _chunks.Add(client, new HashSet<Point2>(current));
        }
        return new ChunkInterestChanges(entered, left);
    }

    public void RetainClients(IEnumerable<Client> clients)
    {
        var retained = new HashSet<Client>(clients, ReferenceEqualityComparer.Instance);
        foreach (var client in _chunks.Keys.Where(client => !retained.Contains(client)).ToArray())
        {
            _chunks.Remove(client);
        }
    }
}
