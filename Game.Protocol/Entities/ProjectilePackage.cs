using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
[Union(0, typeof(ProjectileAddedPackage))]
[Union(1, typeof(ProjectileEffectPackage))]
[Union(2, typeof(ProjectileRemovedPackage))]
[Union(3, typeof(ProjectileStatePackage))]
public abstract record ProjectilePackage : IProtocolMessage
{
    public enum EventType : byte
    {
        Add = 0,

        Remove = 1,

        Update = 2,

        Effect = 3
    }


    public enum EffectType : byte
    {
        Impact = 0,

        Debris = 1,

        WaterSplash = 2,

        MagmaSplash = 3,

        Sizzle = 4
    }

    [IgnoreMember] public abstract EventType Type { get; }

    [Key(0)] public int NetworkId { get; init; }
}
