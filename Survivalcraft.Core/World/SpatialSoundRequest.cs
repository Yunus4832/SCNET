namespace Game.World;

/// <summary>描述本世界待播放的声音，不持有监听者、音频片段或设备对象。</summary>
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
