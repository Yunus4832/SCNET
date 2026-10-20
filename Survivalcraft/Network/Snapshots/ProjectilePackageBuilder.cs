namespace Game.Network.Snapshots;

public static class ProjectilePackageBuilder
{
    public static ProjectilePackage Capture(Projectile p) => new()
    {
        Type = ProjectilePackage.EventType.Add,
        NetworkId = p.NetworkId,
        Value = p.Value,
        Position = p.Position,
        Rotation = p.Rotation,
        Velocity = p.Velocity,
        TrailOffset = p.TrailOffset,
        AngularVelocity = p.AngularVelocity,
        OwnerId = p.Owner?.Entity.EntityId ?? 0,
        IsFireProjectile = p.IsFireProjectile
    };
}
