namespace Game.World;

public readonly record struct ExplosionAcousticProfile(
    string Asset,
    float MinimumDistance,
    float NoiseRadius,
    float PitchVariation
);
