namespace Engine.Core;

public struct Box(int left, int top, int near, int width, int height, int depth) : IEquatable<Box>
{
    public int Left = left;

    public int Top = top;

    public int Near = near;

    public int Width = width;

    public int Height = height;

    public int Depth = depth;

    public static Box Empty;

    public Point3 Location
    {
        get => new(Left, Top, Near);
        set
        {
            Left = value.X;
            Top = value.Y;
            Near = value.Z;
        }
    }

    public Point3 Size
    {
        get => new(Width, Height, Depth);
        set
        {
            Width = value.X;
            Height = value.Y;
            Depth = value.Z;
        }
    }

    public int Right => Left + Width;

    public int Bottom => Top + Height;

    public int Far => Near + Depth;

    public static implicit operator Box((int Left, int Top, int Near, int Width, int Height, int Depth) v)
    {
        return new Box(v.Left, v.Top, v.Near, v.Width, v.Height, v.Depth);
    }

    public bool Equals(Box other)
    {
        if (Left == other.Left && Top == other.Top && Near == other.Near && Width == other.Width &&
            Height == other.Height)
        {
            return Depth == other.Depth;
        }

        return false;
    }

    public override bool Equals(object? obj)
    {
        return obj is Box box && Equals(box);
    }

    public override int GetHashCode()
    {
        return Left + Top + Near + Width + Height + Depth;
    }

    public override string ToString()
    {
        return $"{Left},{Top},{Near},{Width},{Height},{Depth}";
    }

    public bool Contains(Point3 p)
    {
        if (p.X >= Left && p.X < Left + Width && p.Y >= Top && p.Y < Top + Height && p.Z >= Near)
        {
            return p.Z < Near + Depth;
        }

        return false;
    }

    public static Box Intersection(Box b1, Box b2)
    {
        var num = MathUtils.Max(b1.Left, b2.Left);
        var num2 = MathUtils.Max(b1.Top, b2.Top);
        var num3 = MathUtils.Min(b1.Near, b2.Near);
        var num4 = MathUtils.Min(b1.Left + b1.Width, b2.Left + b2.Width);
        var num5 = MathUtils.Min(b1.Top + b1.Height, b2.Top + b2.Height);
        var num6 = MathUtils.Min(b1.Near + b1.Depth, b2.Near + b2.Depth);
        if (num4 <= num || num5 <= num2 || num6 <= num3)
        {
            return Empty;
        }

        return new Box(num, num2, num3, num4 - num, num5 - num2, num6 - num3);
    }

    public static Box Union(Box b1, Box b2)
    {
        var num = MathUtils.Min(b1.Left, b2.Left);
        var num2 = MathUtils.Min(b1.Top, b2.Top);
        var num3 = MathUtils.Min(b1.Near, b2.Near);
        var num4 = MathUtils.Max(b1.Left + b1.Width, b2.Left + b2.Width);
        var num5 = MathUtils.Max(b1.Top + b1.Height, b2.Top + b2.Height);
        var num6 = MathUtils.Max(b1.Near + b1.Depth, b2.Near + b2.Depth);
        return new Box(num, num2, num3, num4 - num, num5 - num2, num6 - num3);
    }

    public static bool operator ==(Box b1, Box b2)
    {
        return b1.Equals(b2);
    }

    public static bool operator !=(Box b1, Box b2)
    {
        return !b1.Equals(b2);
    }
}
