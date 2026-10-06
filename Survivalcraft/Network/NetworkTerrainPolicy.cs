namespace Game.Network;

public static class NetworkTerrainPolicy
{
    public const int DefaultMaxClientVisibilityRange = 512;

    public const int DefaultServerChunkCountSendPer = 100;

    public const int DefaultServerChunkBytesSendPerSecond = 128 * 1024;

    public const float ClientChunkRetentionMargin = 32f;

    public const int ClientRetainedChunkCapacity = 256;

    public const float ClientPredictionSeconds = 0.75f;

    public const float MaximumPredictedClientSpeed = 128f;

    public const float MaximumClientCameraOffset = 64f;

    public static bool IsChunkRelevant(Point2 chunk, Vector2 center, float distance)
    {
        var min = new Vector2(chunk.X * 16f, chunk.Y * 16f);
        var max = min + new Vector2(16f);
        var closest = new Vector2(MathUtils.Clamp(center.X, min.X, max.X),
            MathUtils.Clamp(center.Y, min.Y, max.Y));
        return Vector2.DistanceSquared(center, closest) <= MathUtils.Sqr(distance);
    }

    public static bool TryClampClientUpdateLocation(
        TerrainUpdater.UpdateLocation requested,
        int configuredMaximum,
        Vector2 authoritativeCenter,
        out TerrainUpdater.UpdateLocation clamped)
    {
        clamped = requested;
        if (!float.IsFinite(requested.Center.X) || !float.IsFinite(requested.Center.Y))
        {
            return false;
        }

        if (!float.IsFinite(authoritativeCenter.X) || !float.IsFinite(authoritativeCenter.Y))
        {
            return false;
        }

        var offset = requested.Center - authoritativeCenter;
        if (offset.LengthSquared() > MathUtils.Sqr(MaximumClientCameraOffset))
        {
            clamped.Center = authoritativeCenter +
                             Vector2.Normalize(offset) * MaximumClientCameraOffset;
        }

        if (requested.LastChunksUpdateCenter is { } lastCenter &&
            (!float.IsFinite(lastCenter.X) || !float.IsFinite(lastCenter.Y)))
        {
            clamped.LastChunksUpdateCenter = null;
        }

        var maximum = MathUtils.Clamp(configuredMaximum, 32, ushort.MaxValue);
        clamped.VisibilityDistance = MathUtils.Clamp(requested.VisibilityDistance, 32f, maximum);
        clamped.ContentDistance = MathUtils.Clamp(requested.ContentDistance, clamped.VisibilityDistance, maximum);
        return true;
    }
}
