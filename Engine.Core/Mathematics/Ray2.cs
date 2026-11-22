namespace Engine.Core.Mathematics;

public struct Ray2(Vector2 position, Vector2 direction) : IEquatable<Ray2>
{
    public Vector2 Position = position;

    public Vector2 Direction = direction;

    public override bool Equals(object? obj)
    {
        return obj is Ray2 ray2 && Equals(ray2);
    }

    public float? Intersection(BoundingRectangle rectangle)
    {
        var entryDistance = 0f;
        if (Direction.X == 0f)
        {
            if (Position.X < rectangle.Min.X || Position.X > rectangle.Max.X)
            {
                return null;
            }
        }
        else
        {
            var inverseX = 1f / Direction.X;
            var entryX = (rectangle.Min.X - Position.X) * inverseX;
            var exitX = (rectangle.Max.X - Position.X) * inverseX;
            if (entryX > exitX)
            {
                (entryX, exitX) = (exitX, entryX);
            }

            entryDistance = MathUtils.Max(entryX, entryDistance);
            if (entryDistance > exitX)
            {
                return null;
            }
        }

        if (Direction.Y == 0f)
        {
            if (Position.Y < rectangle.Min.Y || Position.Y > rectangle.Max.Y)
            {
                return null;
            }
        }
        else
        {
            var inverseY = 1f / Direction.Y;
            var entryY = (rectangle.Min.Y - Position.Y) * inverseY;
            var exitY = (rectangle.Max.Y - Position.Y) * inverseY;
            if (entryY > exitY)
            {
                (entryY, exitY) = (exitY, entryY);
            }

            entryDistance = MathUtils.Max(entryY, entryDistance);
            if (entryDistance > exitY)
            {
                return null;
            }
        }

        return entryDistance;
    }

    public float? Intersection(BoundingCircle circle)
    {
        var v = circle.Center - Position;
        var centerDistanceSquared = v.LengthSquared();
        var radiusSquared = circle.Radius * circle.Radius;
        if (centerDistanceSquared < radiusSquared)
        {
            return 0f;
        }

        var projectedDistance = Vector2.Dot(Direction, v);
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

    public Vector2 Sample(float distance)
    {
        return Position + Direction * distance;
    }

    public static Ray2 Transform(Ray2 r, Matrix m)
    {
        Transform(ref r, ref m, out var result);
        return result;
    }

    public static void Transform(ref Ray2 r, ref Matrix m, out Ray2 result)
    {
        Vector2.Transform(ref r.Position, ref m, out result.Position);
        Vector2.TransformNormal(ref r.Direction, ref m, out result.Direction);
    }

    public override int GetHashCode()
    {
        return Position.GetHashCode() + Direction.GetHashCode();
    }

    public override string ToString()
    {
        return $"{Position.ToString()},{Direction.ToString()}";
    }

    public bool Equals(Ray2 other)
    {
        if (Position == other.Position)
        {
            return Direction == other.Direction;
        }

        return false;
    }

    public static bool operator ==(Ray2 a, Ray2 b)
    {
        return a.Equals(b);
    }

    public static bool operator !=(Ray2 a, Ray2 b)
    {
        return !a.Equals(b);
    }
}
