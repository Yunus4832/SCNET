namespace Engine.Core;

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
        var num = 0f;
        if (Direction.X == 0f)
        {
            if (Position.X < rectangle.Min.X || Position.X > rectangle.Max.X)
            {
                return null;
            }
        }
        else
        {
            var num2 = 1f / Direction.X;
            var num3 = (rectangle.Min.X - Position.X) * num2;
            var num4 = (rectangle.Max.X - Position.X) * num2;
            if (num3 > num4)
            {
                (num3, num4) = (num4, num3);
            }

            num = MathUtils.Max(num3, num);
            if (num > num4)
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
            var num6 = 1f / Direction.Y;
            var num7 = (rectangle.Min.Y - Position.Y) * num6;
            var num8 = (rectangle.Max.Y - Position.Y) * num6;
            if (num7 > num8)
            {
                (num7, num8) = (num8, num7);
            }

            num = MathUtils.Max(num7, num);
            if (num > num8)
            {
                return null;
            }
        }

        return num;
    }

    public float? Intersection(BoundingCircle circle)
    {
        var v = circle.Center - Position;
        var num = v.LengthSquared();
        var num2 = circle.Radius * circle.Radius;
        if (num < num2)
        {
            return 0f;
        }

        var num3 = Vector2.Dot(Direction, v);
        if (num3 < 0f)
        {
            return null;
        }

        var num4 = num2 + num3 * num3 - num;
        if (!(num4 < 0f))
        {
            return num3 - MathUtils.Sqrt(num4);
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
