namespace Game.World;

/// <summary>A world-local acoustic intent; it contains no listener, clip or device object.</summary>
public readonly record struct SpatialSoundRequest(
    string Asset,
    bool RandomDirectory,
    float Volume,
    float Pitch,
    Vector3 Position,
    float MinimumDistance,
    bool AutoDelay,
    float DelaySeconds,
    string? NearestGroup = null,
    float PitchVariation = 0f
);
