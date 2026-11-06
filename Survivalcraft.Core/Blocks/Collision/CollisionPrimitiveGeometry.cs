namespace Game.Blocks.Collision;

/// <summary>Immutable indexed position vertices, with no material or rendering resources.</summary>
public sealed class CollisionPrimitiveGeometry(IEnumerable<Vector3> positions)
{
    public IReadOnlyList<Vector3> Positions { get; } = Array.AsReadOnly(positions.ToArray());
}
