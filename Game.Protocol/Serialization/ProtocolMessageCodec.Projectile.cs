using Game.Protocol.Entities;

namespace Game.Protocol.Serialization;

public static partial class ProtocolMessageCodec
{
    private static void WriteProjectilePackage(ProjectilePackage message, ProtocolWriter writer)
    {
        writer.WriteEnum(message.Type);
        writer.Write(message.NetworkId);
        if (message.Type == ProjectilePackage.EventType.Effect)
        {
            writer.WriteEnum(message.Effect);
            writer.Write(message.Value);
            writer.Write(message.Position);
            return;
        }

        if (message.Type == ProjectilePackage.EventType.Remove)
        {
            return;
        }

        writer.Write(message.StateTick);
        writer.Write(message.Position);
        writer.Write(message.Rotation);
        writer.Write(message.Velocity);
        writer.Write(message.AngularVelocity);
        if (message.Type == ProjectilePackage.EventType.Update)
        {
            return;
        }

        writer.Write(message.Value);
        writer.Write(message.TrailOffset);
        writer.Write(message.OwnerId);
        writer.Write(message.IsFireProjectile);
    }

    private static ProjectilePackage ReadProjectilePackage(ProtocolReader reader)
    {
        var type = reader.ReadEnum<ProjectilePackage.EventType>();
        var id = reader.ReadInt32();
        if (type == ProjectilePackage.EventType.Remove) { return new ProjectilePackage(id); }

        if (type == ProjectilePackage.EventType.Effect)
        {
            return new ProjectilePackage
            {
                Type = type, NetworkId = id, Effect = reader.ReadEnum<ProjectilePackage.EffectType>(),
                Value = reader.ReadInt32(), Position = reader.ReadVector3()
            };
        }

        if (type is not (ProjectilePackage.EventType.Add or ProjectilePackage.EventType.Update))
        {
            throw new InvalidDataException("Unknown projectile mode.");
        }

        var tick = reader.ReadUInt32();
        var position = reader.ReadVector3();
        var rotation = reader.ReadVector3();
        var velocity = reader.ReadVector3();
        var angular = reader.ReadVector3();
        if (type == ProjectilePackage.EventType.Update)
        {
            return new ProjectilePackage
            {
                Type = type, NetworkId = id, StateTick = tick, Position = position, Rotation = rotation,
                Velocity = velocity, AngularVelocity = angular
            };
        }

        return new ProjectilePackage
        {
            Type = type,
            NetworkId = id,
            StateTick = tick,
            Position = position,
            Rotation = rotation,
            Velocity = velocity,
            AngularVelocity = angular,
            Value = reader.ReadInt32(),
            TrailOffset = reader.ReadVector3(),
            OwnerId = reader.ReadInt32(),
            IsFireProjectile = reader.ReadBoolean()
        };
    }
}
