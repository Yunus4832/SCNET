namespace Game.Blocks.Collision;

public sealed class CollisionModelGeometry(ResourceId id, IEnumerable<CollisionNodeGeometry> nodes)
{
    public ResourceId Id { get; } = id;

    public IReadOnlyList<CollisionNodeGeometry> Nodes { get; } = Array.AsReadOnly(nodes.ToArray());

    public CollisionNodeGeometry GetNode(string name)
    {
        var matches = Nodes.Where(node => node.Name == name).Take(2).ToArray();
        return matches.Length switch
        {
            1 => matches[0],
            0 => throw new InvalidOperationException($"Collision model {Id} has no node '{name}'."),
            _ => throw new InvalidOperationException($"Collision model {Id} has ambiguous node '{name}'.")
        };
    }
}
