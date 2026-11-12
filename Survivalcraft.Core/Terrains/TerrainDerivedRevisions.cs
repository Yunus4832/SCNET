namespace Game.Terrains;

/// <summary>Independent completion revisions for one world's height and lighting inputs.</summary>
public sealed class TerrainDerivedRevisions
{
    private long _heightRequest = 1;

    private long _lightRequest = 1;

    private long _seedRequest = 1;

    private long _completedHeightRequest;

    private long _completedLightRequest;

    private long _completedSeedRequest;

    private long _heightRevision;

    private long _lightRevision;

    public bool HasCurrentHeights => Volatile.Read(ref _completedHeightRequest) == Volatile.Read(ref _heightRequest);

    public bool HasCurrentLightSeed => HasCurrentHeights &&
                                       Volatile.Read(ref _completedSeedRequest) ==
                                       Volatile.Read(ref _seedRequest);

    public bool HasCurrentLight => HasCurrentLightSeed &&
                                   Volatile.Read(ref _completedLightRequest) == Volatile.Read(ref _lightRequest);

    public long HeightRevision => Volatile.Read(ref _heightRevision);

    public long LightRevision => Volatile.Read(ref _lightRevision);

    public readonly record struct Request(long Heights, long Lighting, long Seed);

    public Request Capture()
    {
        return new Request(
            Volatile.Read(ref _heightRequest),
            Volatile.Read(ref _lightRequest),
            Volatile.Read(ref _seedRequest)
        );
    }

    public void InvalidateHeightsAndLight()
    {
        Interlocked.Increment(ref _heightRequest);
        InvalidateLight();
    }

    public void InvalidateLight()
    {
        Interlocked.Increment(ref _seedRequest);
        Interlocked.Increment(ref _lightRequest);
    }

    public void InvalidatePropagation() => Interlocked.Increment(ref _lightRequest);

    public bool PublishHeights(Request request)
    {
        if (Volatile.Read(ref _heightRequest) != request.Heights) { return false; }

        Volatile.Write(ref _completedHeightRequest, request.Heights);
        Interlocked.Increment(ref _heightRevision);
        return Volatile.Read(ref _heightRequest) == request.Heights;
    }

    public bool PublishLightSeed(Request request)
    {
        if (Volatile.Read(ref _heightRequest) != request.Heights ||
            Volatile.Read(ref _completedHeightRequest) != request.Heights ||
            Volatile.Read(ref _seedRequest) != request.Seed) { return false; }

        Volatile.Write(ref _completedSeedRequest, request.Seed);
        return HasCurrentLightSeed;
    }

    public bool PublishLight(Request request, bool valuesChanged = true)
    {
        if (Volatile.Read(ref _heightRequest) != request.Heights ||
            Volatile.Read(ref _completedHeightRequest) != request.Heights ||
            Volatile.Read(ref _completedSeedRequest) != request.Seed ||
            Volatile.Read(ref _seedRequest) != request.Seed ||
            Volatile.Read(ref _lightRequest) != request.Lighting)
        {
            return false;
        }

        Volatile.Write(ref _completedLightRequest, request.Lighting);
        if (valuesChanged) { Interlocked.Increment(ref _lightRevision); }

        return HasCurrentLight;
    }
}
