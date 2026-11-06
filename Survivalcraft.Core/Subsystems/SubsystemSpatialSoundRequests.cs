using EntitySystem.Core;

namespace Game.Subsystems;

/// <summary>Produces acoustic facts for this world without playing or broadcasting them.</summary>
public sealed class SubsystemSpatialSoundRequests : Subsystem
{
    private bool _disposed;

    public event Action<SpatialSoundRequest>? Requested;

    public void RequestSound(
        string asset,
        float volume,
        float pitch,
        Vector3 position,
        float minimumDistance,
        bool autoDelay
    )
    {
        Publish(new SpatialSoundRequest(asset, false, volume, pitch, position, minimumDistance, autoDelay, 0f));
    }

    public void RequestSound(
        string asset,
        float volume,
        float pitch,
        Vector3 position,
        float minimumDistance,
        float delay
    )
    {
        Publish(new SpatialSoundRequest(asset, false, volume, pitch, position, minimumDistance, false, delay));
    }

    public void RequestRandomSound(
        string directory,
        float volume,
        float pitch,
        Vector3 position,
        float minimumDistance,
        bool autoDelay
    )
    {
        Publish(new SpatialSoundRequest(directory, true, volume, pitch, position, minimumDistance, autoDelay, 0f));
    }

    public void RequestRandomSound(
        string directory,
        float volume,
        float pitch,
        Vector3 position,
        float minimumDistance,
        float delay
    )
    {
        Publish(new SpatialSoundRequest(directory, true, volume, pitch, position, minimumDistance, false, delay));
    }

    public void RequestNearestSound(
        string group,
        string asset,
        float volume,
        float pitchVariation,
        Vector3 position,
        float minimumDistance,
        bool autoDelay
    )
    {
        Publish(
            new SpatialSoundRequest(
                asset,
                false,
                volume,
                0f,
                position,
                minimumDistance,
                autoDelay,
                0f,
                group,
                pitchVariation
            )
        );
    }

    private void Publish(SpatialSoundRequest request)
    {
        if (!_disposed && WorldRuntimeContext.IsProjectActive(Project)) { Requested?.Invoke(request); }
    }

    public override void Dispose()
    {
        _disposed = true;
        Requested = null;
    }
}
