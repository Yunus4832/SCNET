namespace Game;

public struct Segment3(Vector3 start, Vector3 end)
{
    public Vector3 Start = start;

    public Vector3 End = end;

    public override string ToString()
    {
        return $"{Start.X}, {Start.Y}, {Start.Z},  {End.X}, {End.Y}, {End.Z}";
    }

    public static float Distance(Segment3 s, Vector3 p)
    {
        var v = s.End - s.Start;
        var v2 = s.Start - p;
        var v3 = s.End - p;
        var num = Vector3.Dot(v2, v);
        if (!(num * Vector3.Dot(v3, v) <= 0f))
        {
            return !(num > 0f) ? v3.Length() : v2.Length();
        }

        var num2 = v.LengthSquared();
        return num2 == 0f
            ? Vector3.Distance(p, s.Start)
            : MathUtils.Sqrt(Vector3.Cross(p - s.Start, v).LengthSquared() / num2);
    }

    public static Vector3 NearestPoint(Segment3 s, Vector3 p)
    {
        var v = s.End - s.Start;
        var v2 = s.Start - p;
        var v3 = s.End - p;
        var num = Vector3.Dot(v2, v);
        if (!(num * Vector3.Dot(v3, v) <= 0f))
        {
            return !(num > 0f) ? s.End : s.Start;
        }

        var num2 = v.LengthSquared();
        if (num2 == 0f)
        {
            return s.Start;
        }

        var num3 = MathUtils.Sqrt(v2.LengthSquared() - Vector3.Cross(p - s.Start, v).LengthSquared() / num2);
        return Vector3.Lerp(s.Start, s.End, num3 / MathUtils.Sqrt(num2));

    }
}
