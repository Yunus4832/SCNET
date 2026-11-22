namespace Engine.Core.Mathematics;

public struct Rectangle(int left, int top, int width, int height) : IEquatable<Rectangle>
{
    public int Left = left;

    public int Top = top;

    public int Width = width;

    public int Height = height;

    public static Rectangle Empty;

    public Point2 Location
    {
        get => new(Left, Top);
        set
        {
            Left = value.X;
            Top = value.Y;
        }
    }

    public Point2 Size
    {
        get => new(Width, Height);
        set
        {
            Width = value.X;
            Height = value.Y;
        }
    }

    public int Right => Left + Width;

    public int Bottom => Top + Height;

    public static implicit operator Rectangle((int Left, int Top, int Width, int Height) v)
    {
        return new Rectangle(v.Left, v.Top, v.Width, v.Height);
    }

    public bool Equals(Rectangle other)
    {
        if (Left == other.Left && Top == other.Top && Width == other.Width)
        {
            return Height == other.Height;
        }

        return false;
    }

    public override bool Equals(object? obj)
    {
        return obj is Rectangle rectangle && Equals(rectangle);
    }

    public override int GetHashCode()
    {
        return Left + Top + Width + Height;
    }

    public override string ToString()
    {
        return $"{Left},{Top},{Width},{Height}";
    }

    public bool Contains(Point2 p)
    {
        if (p.X >= Left && p.X < Left + Width && p.Y >= Top)
        {
            return p.Y < Top + Height;
        }

        return false;
    }

    public bool Intersection(Rectangle r)
    {
        var left = MathUtils.Max(Left, r.Left);
        var top = MathUtils.Max(Top, r.Top);
        var right = MathUtils.Min(Left + Width, r.Left + r.Width);
        var bottom = MathUtils.Min(Top + Height, r.Top + r.Height);
        if (right > left)
        {
            return bottom > top;
        }

        return false;
    }

    public static Rectangle Intersection(Rectangle r1, Rectangle r2)
    {
        var left = MathUtils.Max(r1.Left, r2.Left);
        var top = MathUtils.Max(r1.Top, r2.Top);
        var right = MathUtils.Min(r1.Left + r1.Width, r2.Left + r2.Width);
        var bottom = MathUtils.Min(r1.Top + r1.Height, r2.Top + r2.Height);
        if (right <= left || bottom <= top)
        {
            return Empty;
        }

        return new Rectangle(left, top, right - left, bottom - top);
    }

    public static Rectangle Union(Rectangle r1, Rectangle r2)
    {
        var left = MathUtils.Min(r1.Left, r2.Left);
        var top = MathUtils.Min(r1.Top, r2.Top);
        var right = MathUtils.Max(r1.Left + r1.Width, r2.Left + r2.Width);
        var bottom = MathUtils.Max(r1.Top + r1.Height, r2.Top + r2.Height);
        return new Rectangle(left, top, right - left, bottom - top);
    }

    public static bool operator ==(Rectangle r1, Rectangle r2)
    {
        return r1.Equals(r2);
    }

    public static bool operator !=(Rectangle r1, Rectangle r2)
    {
        return !r1.Equals(r2);
    }
}
