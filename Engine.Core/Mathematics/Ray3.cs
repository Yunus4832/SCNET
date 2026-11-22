namespace Engine.Core.Mathematics;

public struct Ray3(Vector3 position, Vector3 direction) : IEquatable<Ray3>
{
    public Vector3 Position = position;

    public Vector3 Direction = direction;

    public override bool Equals(object? obj)
    {
        return obj is Ray3 ray3 && Equals(ray3);
    }

    public override int GetHashCode()
    {
        return Position.GetHashCode() + Direction.GetHashCode();
    }

    public override string ToString()
    {
        return $"{Position.ToString()},{Direction.ToString()}";
    }

    public bool Equals(Ray3 other)
    {
        if (Position == other.Position)
        {
            return Direction == other.Direction;
        }

        return false;
    }

    public float? Intersection(BoundingBox box)
    {
        if (Position.X >= box.Min.X &&
            Position.X <= box.Max.X &&
            Position.Y >= box.Min.Y &&
            Position.Y <= box.Max.Y &&
            Position.Z >= box.Min.Z &&
            Position.Z <= box.Max.Z)
        {
            return 0f;
        }

        var vector = new Vector3(-1f);
        if (Direction.X != 0f)
        {
            if (Position.X < box.Min.X)
            {
                vector.X = (box.Min.X - Position.X) / Direction.X;
            }
            else if (Position.X > box.Max.X)
            {
                vector.X = (box.Max.X - Position.X) / Direction.X;
            }
        }

        if (Direction.Y != 0f)
        {
            if (Position.Y < box.Min.Y)
            {
                vector.Y = (box.Min.Y - Position.Y) / Direction.Y;
            }
            else if (Position.Y > box.Max.Y)
            {
                vector.Y = (box.Max.Y - Position.Y) / Direction.Y;
            }
        }

        if (Direction.Z != 0f)
        {
            if (Position.Z < box.Min.Z)
            {
                vector.Z = (box.Min.Z - Position.Z) / Direction.Z;
            }
            else if (Position.Z > box.Max.Z)
            {
                vector.Z = (box.Max.Z - Position.Z) / Direction.Z;
            }
        }

        if (vector.X > vector.Y && vector.X > vector.Z)
        {
            if (vector.X < 0f)
            {
                return null;
            }

            var zAtXPlane = Position.Z + vector.X * Direction.Z;
            if (zAtXPlane < box.Min.Z || zAtXPlane > box.Max.Z)
            {
                return null;
            }

            zAtXPlane = Position.Y + vector.X * Direction.Y;
            if (zAtXPlane < box.Min.Y || zAtXPlane > box.Max.Y)
            {
                return null;
            }

            return vector.X;
        }

        if (vector.Y > vector.X && vector.Y > vector.Z)
        {
            if (vector.Y < 0f)
            {
                return null;
            }

            var zAtYPlane = Position.Z + vector.Y * Direction.Z;
            if (zAtYPlane < box.Min.Z || zAtYPlane > box.Max.Z)
            {
                return null;
            }

            zAtYPlane = Position.X + vector.Y * Direction.X;
            if (zAtYPlane < box.Min.X || zAtYPlane > box.Max.X)
            {
                return null;
            }

            return vector.Y;
        }

        if (vector.Z < 0f)
        {
            return null;
        }

        var xAtZPlane = Position.X + vector.Z * Direction.X;
        if (xAtZPlane < box.Min.X || xAtZPlane > box.Max.X)
        {
            return null;
        }

        xAtZPlane = Position.Y + vector.Z * Direction.Y;
        if (xAtZPlane < box.Min.Y || xAtZPlane > box.Max.Y)
        {
            return null;
        }

        return vector.Z;
    }

    public float? Intersection(BoundingSphere sphere)
    {
        var v = sphere.Center - Position;
        var centerDistanceSquared = v.LengthSquared();
        var radiusSquared = sphere.Radius * sphere.Radius;
        if (centerDistanceSquared < radiusSquared)
        {
            return 0f;
        }

        var projectedDistance = Vector3.Dot(Direction, v);
        if (projectedDistance < 0f)
        {
            return null;
        }

        var discriminant = radiusSquared + projectedDistance * projectedDistance - centerDistanceSquared;
        if (!(discriminant < 0f))
        {
            return projectedDistance - MathUtils.Sqrt(discriminant);
        }

        return null;
    }

    public float? Intersection(Plane plane)
    {
        var normalDotDirection = Vector3.Dot(Direction, plane.Normal);
        if (normalDotDirection == 0f)
        {
            return null;
        }

        return (0f - (Vector3.Dot(Position, plane.Normal) + plane.D)) / normalDotDirection;
    }

    public Vector3 Sample(float distance)
    {
        return Position + Direction * distance;
    }

    public static Ray3 Transform(Ray3 r, Matrix m)
    {
        Transform(ref r, ref m, out var result);
        return result;
    }

    public static void Transform(ref Ray3 r, ref Matrix m, out Ray3 result)
    {
        Vector3.Transform(ref r.Position, ref m, out result.Position);
        Vector3.TransformNormal(ref r.Direction, ref m, out result.Direction);
    }

    public static bool operator ==(Ray3 a, Ray3 b)
    {
        return a.Equals(b);
    }

    public static bool operator !=(Ray3 a, Ray3 b)
    {
        return !a.Equals(b);
    }
}
