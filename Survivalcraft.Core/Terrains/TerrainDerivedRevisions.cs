namespace Game.Terrains;

/// <summary>Independent completion revisions for one world's height and lighting inputs.</summary>
public sealed class TerrainDerivedRevisions
{
    private long _heightRequest = 1;

    private long _lightRequest = 1;

    private long _completedHeightRequest;

    private long _completedLightRequest;

    private long _completedSunlightRequest;

    private long _heightRevision;

    private long _lightRevision;

    private int _lightWriters;

    public bool HasCurrentHeights => Volatile.Read(ref _completedHeightRequest) == Volatile.Read(ref _heightRequest);

    public bool HasCurrentSunlight => HasCurrentHeights &&
                                      Volatile.Read(ref _completedSunlightRequest) == Volatile.Read(ref _lightRequest);

    public bool HasCompletedLightInputs => HasCurrentSunlight &&
                                           Volatile.Read(ref _completedLightRequest) ==
                                           Volatile.Read(ref _lightRequest);

    public bool HasCurrentLight => HasCompletedLightInputs && Volatile.Read(ref _lightWriters) == 0;

    public long HeightRevision => Volatile.Read(ref _heightRevision);

    public long LightRevision => Volatile.Read(ref _lightRevision);

    public readonly record struct Request(long Heights, long Lighting);

    public Request Capture() => new(Volatile.Read(ref _heightRequest), Volatile.Read(ref _lightRequest));

    public void InvalidateHeightsAndLight()
    {
        Interlocked.Increment(ref _heightRequest);
        Interlocked.Increment(ref _lightRequest);
    }

    public void InvalidateLight() => Interlocked.Increment(ref _lightRequest);

    public bool PublishHeights(Request request)
    {
        if (Volatile.Read(ref _heightRequest) != request.Heights) { return false; }

        Volatile.Write(ref _completedHeightRequest, request.Heights);
        Interlocked.Increment(ref _heightRevision);
        return Volatile.Read(ref _heightRequest) == request.Heights;
    }

    public bool PublishSunlight(Request request)
    {
        if (Volatile.Read(ref _heightRequest) != request.Heights ||
            Volatile.Read(ref _completedHeightRequest) != request.Heights ||
            Volatile.Read(ref _lightRequest) != request.Lighting) { return false; }

        Volatile.Write(ref _completedSunlightRequest, request.Lighting);
        return HasCurrentSunlight;
    }

    public bool PublishLight(Request request)
    {
        if (Volatile.Read(ref _heightRequest) != request.Heights ||
            Volatile.Read(ref _completedHeightRequest) != request.Heights ||
            Volatile.Read(ref _completedSunlightRequest) != request.Lighting ||
            Volatile.Read(ref _lightRequest) != request.Lighting)
        {
            return false;
        }

        Volatile.Write(ref _completedLightRequest, request.Lighting);
        Interlocked.Increment(ref _lightRevision);
        return HasCompletedLightInputs;
    }

    public void BeginLightWrite() => Interlocked.Increment(ref _lightWriters);

    public void EndLightWrite()
    {
        if (Interlocked.Decrement(ref _lightWriters) < 0)
        {
            throw new InvalidOperationException("Terrain light write scope was released twice.");
        }
    }

    /// <summary>Propagation can change a neighbor that already published its own light task.</summary>
    public void RecordLightChange() => Interlocked.Increment(ref _lightRevision);
}
