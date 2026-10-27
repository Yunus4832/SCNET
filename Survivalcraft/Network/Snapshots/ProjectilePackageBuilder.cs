namespace Game.Network.Snapshots;

public static class ProjectilePackageBuilder
{
    public static ProjectileAddedPackage Capture(Projectile p) => new()
    {
        NetworkId = p.NetworkId, Value = p.Value, Position = p.Position, Rotation = p.Rotation, Velocity = p.Velocity,
        TrailOffset = p.TrailOffset, AngularVelocity = p.AngularVelocity, OwnerId = p.Owner?.Entity.EntityId ?? 0,
        IsFireProjectile = p.IsFireProjectile
    };

    public static ProjectileStatePackage CaptureState(Projectile p) => new()
    {
        NetworkId = p.NetworkId, Position = p.Position, Rotation = p.Rotation,
        Velocity = p.Velocity, AngularVelocity = p.AngularVelocity
    };
}
