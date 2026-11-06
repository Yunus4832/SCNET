namespace Game.World;

public static class ExplosionAcoustics
{
    public static Point3? SelectOrigin(IEnumerable<KeyValuePair<Point3, float>> pressures)
    {
        Point3? result = null;
        var peak = 0f;
        foreach (var (point, pressure) in pressures)
        {
            if (pressure > peak ||
                pressure == peak && pressure > 0f && result is { } previous && ComesBefore(point, previous))
            {
                peak = pressure;
                result = point;
            }
        }

        return result;
    }

    private static bool ComesBefore(Point3 value, Point3 other)
    {
        return value.X < other.X ||
               value.X == other.X &&
               (value.Y < other.Y || value.Y == other.Y && value.Z < other.Z);
    }

    public static ExplosionAcousticProfile ForPressure(float level)
    {
        return level switch
        {
            > 1000000f => new("Audio/ExplosionEnormous", 40f, 100f, .1f),
            > 100000f => new("Audio/ExplosionHuge", 30f, 70f, .2f),
            > 20000f => new("Audio/ExplosionLarge", 26f, 50f, .2f),
            > 4000f => new("Audio/ExplosionMedium", 24f, 40f, .2f),
            > 100f => new("Audio/ExplosionSmall", 22f, 35f, .2f),
            > 0f => new("Audio/ExplosionTiny", 20f, 30f, .2f),
            _ => new(string.Empty, 0f, 0f, 0f)
        };
    }
}
