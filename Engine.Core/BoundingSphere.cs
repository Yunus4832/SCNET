namespace Engine.Core;

public struct BoundingSphere(Vector3 center, float radius) : IEquatable<BoundingSphere>
{
    public Vector3 Center = center;

    public readonly float Radius = radius;

    public override bool Equals(object? obj)
    {
        return obj is BoundingSphere sphere && Equals(sphere);
    }

    public override int GetHashCode()
    {
        return Center.GetHashCode() + Radius.GetHashCode();
    }

    public bool Equals(BoundingSphere other)
    {
        return Center == other.Center && Radius.CloseTo(other.Radius);
    }

    public override string ToString()
    {
        return $"{Center},{Radius}";
    }

    public static bool operator ==(BoundingSphere s1, BoundingSphere s2)
    {
        return s1.Equals(s2);
    }

    public static bool operator !=(BoundingSphere s1, BoundingSphere s2)
    {
        return !s1.Equals(s2);
    }
}
