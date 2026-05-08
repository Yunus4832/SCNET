namespace Engine.Core;

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
        var num = MathUtils.Max(Left, r.Left);
        var num2 = MathUtils.Max(Top, r.Top);
        var num3 = MathUtils.Min(Left + Width, r.Left + r.Width);
        var num4 = MathUtils.Min(Top + Height, r.Top + r.Height);
        if (num3 > num)
        {
            return num4 > num2;
        }

        return false;
    }

    public static Rectangle Intersection(Rectangle r1, Rectangle r2)
    {
        var num = MathUtils.Max(r1.Left, r2.Left);
        var num2 = MathUtils.Max(r1.Top, r2.Top);
        var num3 = MathUtils.Min(r1.Left + r1.Width, r2.Left + r2.Width);
        var num4 = MathUtils.Min(r1.Top + r1.Height, r2.Top + r2.Height);
        if (num3 <= num || num4 <= num2)
        {
            return Empty;
        }

        return new Rectangle(num, num2, num3 - num, num4 - num2);
    }

    public static Rectangle Union(Rectangle r1, Rectangle r2)
    {
        var num = MathUtils.Min(r1.Left, r2.Left);
        var num2 = MathUtils.Min(r1.Top, r2.Top);
        var num3 = MathUtils.Max(r1.Left + r1.Width, r2.Left + r2.Width);
        var num4 = MathUtils.Max(r1.Top + r1.Height, r2.Top + r2.Height);
        return new Rectangle(num, num2, num3 - num, num4 - num2);
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
