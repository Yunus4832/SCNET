namespace Engine.Core.Mathematics;

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
        var projection = Vector2.Dot(v2, v);
        if (!(projection * Vector2.Dot(v3, v) <= 0f))
        {
            return !(projection > 0f) ? v3.Length() : v2.Length();
        }

        var segmentLengthSquared = v.LengthSquared();
        if (segmentLengthSquared == 0f)
        {
            return Vector2.Distance(p, s.Start);
        }

        return MathUtils.Abs(Vector2.Cross(p - s.Start, v)) / MathUtils.Sqrt(segmentLengthSquared);
    }

    public static bool Intersection(Segment2 s1, Segment2 s2, out Vector2 result)
    {
        var v = s1.End - s1.Start;
        var v2 = s2.End - s2.Start;
        var determinant = Vector2.Cross(v, v2);
        if (determinant == 0f)
        {
            result = Vector2.Zero;
            return false;
        }

        var inverseDeterminant = 1f / determinant;
        var firstFraction = (v2.X * (s1.Start.Y - s2.Start.Y) - v2.Y * (s1.Start.X - s2.Start.X)) * inverseDeterminant;
        var secondFraction = (v.X * (s1.Start.Y - s2.Start.Y) - v.Y * (s1.Start.X - s2.Start.X)) * inverseDeterminant;
        if (firstFraction < 0f || firstFraction > 1f || secondFraction < 0f || secondFraction > 1f)
        {
            result = Vector2.Zero;
            return false;
        }

        result = new Vector2(s1.Start.X + firstFraction * v.X, s1.Start.Y + firstFraction * v.Y);
        return true;
    }

    public static Vector2 NearestPoint(Segment2 s, Vector2 p)
    {
        var v = s.End - s.Start;
        var v2 = s.Start - p;
        var v3 = s.End - p;
        var projection = Vector2.Dot(v2, v);
        if (!(projection * Vector2.Dot(v3, v) <= 0f))
        {
            return !(projection > 0f) ? s.End : s.Start;
        }

        var segmentLength = v.Length();
        if (segmentLength == 0f)
        {
            return s.Start;
        }

        var projectedLength = MathUtils.Sqrt(
            v2.LengthSquared() -
            MathUtils.Sqr(MathUtils.Abs(Vector2.Cross(p - s.Start, v)) / segmentLength)
        );
        return Vector2.Lerp(s.Start, s.End, projectedLength / segmentLength);
    }
}
