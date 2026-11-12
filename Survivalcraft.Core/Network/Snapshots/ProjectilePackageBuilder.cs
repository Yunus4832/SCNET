namespace Game.Network.Snapshots;

public static class ProjectilePackageBuilder
{
    public static ProjectileAddedPackage Capture(Projectile p) => new()
    {
        NetworkId = p.NetworkId, Value = p.Value, Position = p.Position, Rotation = p.Rotation, Velocity = p.Velocity,
        TrailOffset = p.Trail?.Offset ?? Vector3.Zero, AngularVelocity = p.AngularVelocity,
        OwnerId = p.Owner?.Entity.EntityId ?? 0,
        IsFireProjectile = p.IsFireProjectile, IsResting = p.IsResting, SmokeTrail =
            p.Trail is { Kind: ProjectileTrailKind.Smoke, PublishSmokeSnapshot: true } smoke
                ? new ProjectileSmokeTrailSnapshot { Size = smoke.Size, EndTime = smoke.EndTime, Color = smoke.Color }
                : null
    };

    public static ProjectileStatePackage CaptureState(Projectile p) => new()
    {
        NetworkId = p.NetworkId, Position = p.Position, Rotation = p.Rotation,
        Velocity = p.Velocity, AngularVelocity = p.AngularVelocity, IsResting = p.IsResting
    };
}
