namespace Engine.Core.Mathematics;

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
        var projection = Vector3.Dot(v2, v);
        if (!(projection * Vector3.Dot(v3, v) <= 0f))
        {
            return !(projection > 0f) ? v3.Length() : v2.Length();
        }

        var segmentLengthSquared = v.LengthSquared();
        return segmentLengthSquared == 0f
            ? Vector3.Distance(p, s.Start)
            : MathUtils.Sqrt(Vector3.Cross(p - s.Start, v).LengthSquared() / segmentLengthSquared);
    }

    public static Vector3 NearestPoint(Segment3 s, Vector3 p)
    {
        var v = s.End - s.Start;
        var v2 = s.Start - p;
        var v3 = s.End - p;
        var projection = Vector3.Dot(v2, v);
        if (!(projection * Vector3.Dot(v3, v) <= 0f))
        {
            return !(projection > 0f) ? s.End : s.Start;
        }

        var segmentLengthSquared = v.LengthSquared();
        if (segmentLengthSquared == 0f)
        {
            return s.Start;
        }

        var projectedLength = MathUtils.Sqrt(
            v2.LengthSquared() - Vector3.Cross(p - s.Start, v).LengthSquared() / segmentLengthSquared
        );
        return Vector3.Lerp(s.Start, s.End, projectedLength / MathUtils.Sqrt(segmentLengthSquared));
    }
}
