namespace Game.World;

public sealed record ProjectileTrailDescription(
    ProjectileTrailKind Kind,
    Vector3 Offset,
    int ParticleCount,
    float Size,
    double EndTime,
    Color Color,
    bool PublishSmokeSnapshot = false
);
