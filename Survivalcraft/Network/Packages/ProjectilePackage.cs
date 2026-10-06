using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public class ProjectilePackage : IPackage
{
    public enum EventType : byte
    {
        Add,
        Remove,
        Update,
        Effect
    }

    public enum EffectType : byte
    {
        Impact,
        Debris,
        WaterSplash,
        MagmaSplash,
        Sizzle
    }

    public EffectType Effect;

    public enum ProjectileTailInfo : byte
    {
        None = 0,
        Smoke = 1,
        Fireworks = 2,
        IsOffsetZero = 4
    }

    public Vector3 AngularVelocity;

    public bool IsFireProjectile;

    public int NetworkId;

    public int OwnerId;

    public Vector3 Position;

    public Vector3 Rotation;

    public uint StateTick;

    public Vector3 TrailOffset;

    public int Value;

    public EventType Type;

    public Vector3 Velocity;

    public byte ID => (byte)PackageType.Projectile;




    public ClientState MinNeedState => ClientState.ProjectLoaded;

    public ProjectilePackage()
    {
    }

    public ProjectilePackage(Projectile projectile)
    {
        Type = EventType.Add;
        NetworkId = projectile.NetworkId;
        Value = projectile.Value;
        Position = projectile.Position;
        Rotation = projectile.Rotation;
        Velocity = projectile.Velocity;
        TrailOffset = projectile.TrailOffset;
        AngularVelocity = projectile.AngularVelocity;
        OwnerId = projectile.Owner == null ? 0 : projectile.Owner.Entity.EntityId;
        IsFireProjectile = projectile.IsFireProjectile;
    }

    public ProjectilePackage(int networkId)
    {
        Type = EventType.Remove;
        NetworkId = networkId;
    }


    public void WriteData(PackageStreamWriter writer)
    {
        writer.WriteEnum(Type);
        writer.Write(NetworkId);
        if (Type == EventType.Effect)
        {
            writer.WriteEnum(Effect);
            writer.Write(Value);
            writer.Write(Position);
            return;
        }
        if (Type == EventType.Remove)
        {
            return;
        }

        writer.Write(StateTick);
        writer.Write(Position);
        writer.Write(Rotation);
        writer.Write(Velocity);
        writer.Write(AngularVelocity);
        if (Type == EventType.Update)
        {
            return;
        }

        writer.Write(Value);
        writer.Write(TrailOffset);
        writer.Write(OwnerId);
        writer.Write(IsFireProjectile);
    }

    public void ReadData(PackageStreamReader reader)
    {
        Type = reader.ReadEnum<EventType>();
        NetworkId = reader.ReadInt32();
        if (Type == EventType.Effect)
        {
            Effect = reader.ReadEnum<EffectType>();
            Value = reader.ReadInt32();
            Position = reader.ReadVector3();
            return;
        }
        if (Type == EventType.Remove)
        {
            return;
        }

        StateTick = reader.ReadUInt32();
        Position = reader.ReadVector3();
        Rotation = reader.ReadVector3();
        Velocity = reader.ReadVector3();
        AngularVelocity = reader.ReadVector3();
        if (Type == EventType.Update)
        {
            return;
        }

        Value = reader.ReadInt32();
        TrailOffset = reader.ReadVector3();
        OwnerId = reader.ReadInt32();
        IsFireProjectile = reader.ReadBoolean();
    }
}
