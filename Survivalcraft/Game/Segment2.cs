namespace Game;

public struct Segment2(Vector2 start, Vector2 end)
{
    public Vector2 Start = start;

    public Vector2 End = end;

    public override string ToString()
    {
        return $"{Start.X}, {Start.Y},  {End.X}, {End.Y}";
    }

    public static float Distance(Segment2 s, Vector2 p)
    {
        var v = s.End - s.Start;
        var v2 = s.Start - p;
        var v3 = s.End - p;
        var num = Vector2.Dot(v2, v);
        if (!(num * Vector2.Dot(v3, v) <= 0f))
        {
            return !(num > 0f) ? v3.Length() : v2.Length();
        }

        var num2 = v.LengthSquared();
        if (num2 == 0f)
        {
            return Vector2.Distance(p, s.Start);
        }

        return MathUtils.Abs(Vector2.Cross(p - s.Start, v)) / MathUtils.Sqrt(num2);
    }

    public static bool Intersection(Segment2 s1, Segment2 s2, out Vector2 result)
    {
        var v = s1.End - s1.Start;
        var v2 = s2.End - s2.Start;
        var num = Vector2.Cross(v, v2);
        if (num == 0f)
        {
            result = Vector2.Zero;
            return false;
        }

        var num2 = 1f / num;
        var num3 = (v2.X * (s1.Start.Y - s2.Start.Y) - v2.Y * (s1.Start.X - s2.Start.X)) * num2;
        var num4 = (v.X * (s1.Start.Y - s2.Start.Y) - v.Y * (s1.Start.X - s2.Start.X)) * num2;
        if (num3 < 0f || num3 > 1f || num4 < 0f || num4 > 1f)
        {
            result = Vector2.Zero;
            return false;
        }

        result = new Vector2(s1.Start.X + num3 * v.X, s1.Start.Y + num3 * v.Y);
        return true;
    }

    public static Vector2 NearestPoint(Segment2 s, Vector2 p)
    {
        var v = s.End - s.Start;
        var v2 = s.Start - p;
        var v3 = s.End - p;
        var num = Vector2.Dot(v2, v);
        if (!(num * Vector2.Dot(v3, v) <= 0f))
        {
            return !(num > 0f) ? s.End : s.Start;
        }

        var num2 = v.Length();
        if (num2 == 0f)
        {
            return s.Start;
        }

        var num3 = MathUtils.Sqrt(v2.LengthSquared() -
                                  MathUtils.Sqr(MathUtils.Abs(Vector2.Cross(p - s.Start, v)) / num2));
        return Vector2.Lerp(s.Start, s.End, num3 / num2);
    }
}
