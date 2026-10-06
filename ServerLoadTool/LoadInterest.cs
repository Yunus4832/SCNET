using Engine.Core;

namespace ServerLoadTool;

public static class LoadInterest
{
    public static bool ShouldMove(Vector2? current, Vector2 requested) =>
        !current.HasValue || Vector2.DistanceSquared(current.Value, requested) > 64;

    public static HashSet<Point2> Chunks(Vector2 center, int distance)
    {
        var result = new HashSet<Point2>();
        var radius = distance / 16 + 1;
        var coords = new Point2((int)Math.Floor(center.X / 16), (int)Math.Floor(center.Y / 16));
        for (var x = -radius; x <= radius; x++)
        {
            for (var z = -radius; z <= radius; z++)
            {
                var candidate = new Point2(coords.X + x, coords.Y + z);
                var chunkCenter = new Vector2((candidate.X + 0.5f) * 16, (candidate.Y + 0.5f) * 16);
                if (Vector2.DistanceSquared(chunkCenter, center) <= distance * distance)
                {
                    result.Add(candidate);
                }
            }
        }

        return result;
    }
}
