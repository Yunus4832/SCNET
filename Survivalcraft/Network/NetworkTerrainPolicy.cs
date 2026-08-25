namespace Game.Network;

public static class NetworkTerrainPolicy
{
    public const int DefaultMaxClientVisibilityRange = 512;

    public const int DefaultServerChunkCountSendPer = 100;

    public const int DefaultServerChunkBytesSendPerSecond = 128 * 1024;

    public const int MaxTerrainReliablePacketsInQueue = 32;

    public const float ClientChunkRetentionMargin = 32f;

    public const int ClientRetainedChunkCapacity = 256;

    public const float ClientPredictionSeconds = 0.75f;

    public const float MaximumPredictedClientSpeed = 128f;

    public static bool TryClampClientUpdateLocation(
        TerrainUpdater.UpdateLocation requested,
        int configuredMaximum,
        out TerrainUpdater.UpdateLocation clamped)
    {
        clamped = requested;
        if (!float.IsFinite(requested.Center.X) || !float.IsFinite(requested.Center.Y))
        {
            return false;
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
