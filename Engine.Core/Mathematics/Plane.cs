namespace Engine.Core.Mathematics;

public struct Plane : IEquatable<Plane>
{
    public Vector3 Normal;

    public float D;

    public Plane(Vector4 v)
    {
        this = new Plane(new Vector3(v.X, v.Y, v.Z), v.W);
    }

    public Plane(Vector3 normal, float d)
    {
        Normal = normal;
        D = d;
    }

    public Plane(Vector3 a, Vector3 b, Vector3 c)
    {
        Normal = Vector3.Normalize(Vector3.Cross(b - a, c - a));
        D = 0f - Vector3.Dot(Normal, a);
    }

    public Plane(float x, float y, float z, float d)
    {
        this = new Plane(new Vector3(x, y, z), d);
    }

    public override bool Equals(object? obj)
    {
        return obj is Plane plane && Equals(plane);
    }

    public bool Equals(Plane other)
    {
        return Normal == other.Normal && D.CloseTo(other.D);
    }

    public override int GetHashCode()
    {
        return Normal.GetHashCode() + D.GetHashCode();
    }

    public override string ToString()
    {
        return $"{Normal.X},{Normal.Y},{Normal.Z},{D}";
    }

    public static Plane Normalize(Plane p)
    {
        var normalLength = p.Normal.Length();
        if (!(normalLength > 0f))
        {
            return new Plane(Vector3.UnitX, 0f);
        }

        var inverseLength = 1f / normalLength;
        return new Plane(p.Normal * inverseLength, p.D * inverseLength);
    }

    public static bool operator ==(Plane p1, Plane p2)
    {
        return p1.Equals(p2);
    }

    public static bool operator !=(Plane p1, Plane p2)
    {
        return !p1.Equals(p2);
    }
}
