namespace Game.Subsystems;

public readonly record struct WeaponSoundRequest(
    string Name,
    float Volume,
    float Pitch,
    Vector3 Position,
    float MinDistance,
    float? Delay
);
