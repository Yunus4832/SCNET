using Game.Network.Chunks;

namespace Game.Terrains.Rendering;

/// <summary>Per-view visibility constrained by the continuous coverage of drawable terrain.</summary>
internal sealed class TerrainVisibilityBoundary
{
    internal const float coveragePadding = 0.05f;

    private const float _expansionTime = 0.12f;

    private bool _initialized;

    private Vector2 _previousCenter;

    public float SafeDistance { get; private set; }

    public float DisplayDistance { get; private set; }

    public void Update(Vector2 center, float targetDistance, float elapsed, Func<Point2, bool> isDrawable)
    {
        SafeDistance = CalculateSafeDistance(center, targetDistance, isDrawable);
        if (!_initialized || Vector2.DistanceSquared(center, _previousCenter) > targetDistance * targetDistance)
        {
            DisplayDistance = SafeDistance;
            _initialized = true;
        }
        else
        {
            var amount = 1f - MathF.Exp(-MathF.Max(elapsed, 0f) / _expansionTime);
            DisplayDistance = MathF.Min(SafeDistance, MathUtils.Lerp(DisplayDistance, SafeDistance, amount));
            if (SafeDistance - DisplayDistance < 0.01f) { DisplayDistance = SafeDistance; }
        }

        _previousCenter = center;
    }

    internal static float CalculateSafeDistance(Vector2 center, float targetDistance, Func<Point2, bool> isDrawable)
    {
        if (!float.IsFinite(center.X) ||
            !float.IsFinite(center.Y) ||
            !float.IsFinite(targetDistance) ||
            targetDistance < 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(targetDistance),
                "Visibility inputs must be finite and nonnegative."
            );
        }

        if (targetDistance == 0f) { return 0f; }

        var minimum = Terrain.ToChunk(center - new Vector2(targetDistance));
        var maximum = Terrain.ToChunk(center + new Vector2(targetDistance));
        var distanceSquared = targetDistance * targetDistance;
        var missing = false;
        for (var x = minimum.X; x <= maximum.X; x++)
        {
            for (var z = minimum.Y; z <= maximum.Y; z++)
            {
                var coords = new Point2(x, z);
                var chunkCenter = new Vector2(x * 16f + 8f, z * 16f + 8f);
                var candidate = NetworkTerrainPolicy.GetChunkDistanceSquared(chunkCenter, center);
                if (candidate > distanceSquared || isDrawable(coords)) { continue; }

                if (candidate == 0f) { return 0f; }

                distanceSquared = candidate;
                missing = true;
            }
        }

        return missing ? MathF.Max(MathF.Sqrt(distanceSquared) - coveragePadding, 0f) : targetDistance;
    }
}
