namespace Game.Blocks.Collision;

public sealed class CollisionNodeGeometry(
    string name,
    int sourceNodeIndex,
    Matrix absoluteTransform,
    IEnumerable<CollisionPrimitiveGeometry> primitives
)
{
    public string Name { get; } = name;

    public int SourceNodeIndex { get; } = sourceNodeIndex;

    public Matrix AbsoluteTransform { get; } = absoluteTransform;

    public IReadOnlyList<CollisionPrimitiveGeometry> Primitives { get; } = Array.AsReadOnly(primitives.ToArray());
}
