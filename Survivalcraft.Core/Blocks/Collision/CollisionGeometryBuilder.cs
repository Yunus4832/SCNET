namespace Game.Blocks.Collision;

/// <summary>Local working vertices used while deriving one rule definition's collision shapes.</summary>
public sealed class CollisionGeometryBuilder
{
    private readonly List<Vector3> _positions = [];

    public int Count => _positions.Count;

    public void Append(CollisionPrimitiveGeometry primitive, Matrix transform)
    {
        foreach (var position in primitive.Positions)
        {
            _positions.Add(Vector3.Transform(position, transform));
        }
    }

    public void Append(CollisionGeometryBuilder geometry)
    {
        _positions.AddRange(geometry._positions);
    }

    public BoundingBox GetBounds() => new(_positions);

    public BoundingBox GetBounds(Matrix transform)
    {
        return new BoundingBox(_positions.Select(position => Vector3.Transform(position, transform)));
    }
}
