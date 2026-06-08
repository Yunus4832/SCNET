using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public class ProjectilePackage : IPackage
{
    public enum ProjectileTailInfo : byte
    {
        None = 0,
        Smoke = 1,
        Fireworks = 2,
        IsOffsetZero = 4
    }

    public Vector3 AngularVelocity;

    public bool IsFireProjectile;

    public int OwnerId;

    public Vector3 Position;

    public Vector3 TrailOffset;

    public int Value;

    public Vector3 Velocity;

    public byte ID => (byte)PackageType.Projectile;

    public Client? To { get; set; }

    public Client? Except { get; set; }

    public Client? From { get; set; }

    public ClientState MinNeedState => ClientState.ProjectLoaded;

    public ProjectilePackage()
    {
    }

    public ProjectilePackage(Projectile projectile)
    {
        Value = projectile.Value;
        Position = projectile.Position;
        Velocity = projectile.Velocity;
        TrailOffset = projectile.TrailOffset;
        AngularVelocity = projectile.AngularVelocity;
        OwnerId = projectile.Owner == null ? 0 : projectile.Owner.Entity.EntityId;
        IsFireProjectile = projectile.IsFireProjectile;
    }


    public void WriteData(PackageStreamWriter writer)
    {
        writer.Write(Value);
        writer.Write(Position);
        writer.Write(Velocity);
        writer.Write(TrailOffset);
        writer.Write(AngularVelocity);
        writer.Write(OwnerId);
        writer.Write(IsFireProjectile);
    }

    public void ReadData(PackageStreamReader reader)
    {
        Value = reader.ReadInt32();
        Position = reader.ReadVector3();
        Velocity = reader.ReadVector3();
        TrailOffset = reader.ReadVector3();
        AngularVelocity = reader.ReadVector3();
        OwnerId = reader.ReadInt32();
        IsFireProjectile = reader.ReadBoolean();
    }


}
