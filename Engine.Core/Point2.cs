namespace Engine.Core;

public struct Point2(int x, int y) : IEquatable<Point2>
{
    public int X = x;

    public int Y = y;

    public static readonly Point2 Zero = default;

    public static readonly Point2 One = new(1, 1);

    public static readonly Point2 UnitX = new(1, 0);

    public static readonly Point2 UnitY = new(0, 1);

    public Point2(int v) : this(v, v)
    {
    }

    public static implicit operator Point2((int X, int Y) v)
    {
        return new Point2(v.X, v.Y);
    }

    public override int GetHashCode()
    {
        return X + Y;
    }

    public override bool Equals(object? obj)
    {
        return obj is Point2 point2 && Equals(point2);
    }

    public bool Equals(Point2 other)
    {
        if (other.X == X)
        {
            return other.Y == Y;
        }

        return false;
    }

    public override string ToString()
    {
        return $"{X},{Y}";
    }

    public static Point2 Min(Point2 p, int v)
    {
        return new Point2(MathUtils.Min(p.X, v), MathUtils.Min(p.Y, v));
    }

    public static Point2 Min(Point2 p1, Point2 p2)
    {
        return new Point2(MathUtils.Min(p1.X, p2.X), MathUtils.Min(p1.Y, p2.Y));
    }

    public static Point2 Max(Point2 p, int v)
    {
        return new Point2(MathUtils.Max(p.X, v), MathUtils.Max(p.Y, v));
    }

    public static Point2 Max(Point2 p1, Point2 p2)
    {
        return new Point2(MathUtils.Max(p1.X, p2.X), MathUtils.Max(p1.Y, p2.Y));
    }

    public static bool operator ==(Point2 p1, Point2 p2)
    {
        return p1.Equals(p2);
    }

    public static bool operator !=(Point2 p1, Point2 p2)
    {
        return !p1.Equals(p2);
    }

    public static Point2 operator +(Point2 p)
    {
        return p;
    }

    public static Point2 operator -(Point2 p)
    {
        return new Point2(-p.X, -p.Y);
    }

    public static Point2 operator +(Point2 p1, Point2 p2)
    {
        return new Point2(p1.X + p2.X, p1.Y + p2.Y);
    }

    public static Point2 operator -(Point2 p1, Point2 p2)
    {
        return new Point2(p1.X - p2.X, p1.Y - p2.Y);
    }

    public static Point2 operator *(int n, Point2 p)
    {
        return new Point2(p.X * n, p.Y * n);
    }

    public static Point2 operator *(Point2 p, int n)
    {
        return new Point2(p.X * n, p.Y * n);
    }

    public static Point2 operator *(float n, Point2 p)
    {
        return Round(p.X * n, p.Y * n);
    }

    public static Point2 operator *(Point2 p, float n)
    {
        return Round(p.X * n, p.Y * n);
    }


    public static Point2 operator *(Point2 p1, Point2 p2)
    {
        return new Point2(p1.X * p2.X, p1.Y * p2.Y);
    }

    public static Point2 operator /(Point2 p, int n)
    {
        return new Point2(p.X / n, p.Y / n);
    }

    public static Point2 operator /(Point2 p1, Point2 p2)
    {
        return new Point2(p1.X / p2.X, p1.Y / p2.Y);
    }

    public static implicit operator Vector2(Point2 p) => new(p.X, p.Y);

    public static Point2 Round(Vector2 v) => new((int)MathF.Round(v.X), (int)MathF.Round(v.Y));

    public static Point2 Round(float x, float y) => new((int)MathF.Round(x), (int)MathF.Round(y));

    public static Point2 Ceiling(Vector2 v) => new((int)MathF.Ceiling(v.X), (int)MathF.Ceiling(v.Y));

    public static Point2 Ceiling(float x, float y) => new((int)MathF.Ceiling(x), (int)MathF.Ceiling(y));

    public static Point2 Floor(Vector2 v) => new((int)MathF.Floor(v.X), (int)MathF.Floor(v.Y));

    public static Point2 Floor(float x, float y) => new((int)MathF.Floor(x), (int)MathF.Floor(y));
}
