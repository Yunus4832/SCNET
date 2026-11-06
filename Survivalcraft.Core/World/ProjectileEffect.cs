namespace Game.World;

public readonly record struct ProjectileEffect(
    ProjectilePackage.EffectType Kind,
    int Value,
    Vector3 Position,
    float Pitch
);
