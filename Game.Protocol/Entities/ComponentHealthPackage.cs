using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
[Union(0, typeof(CharacterHitEffectPackage))]
[Union(1, typeof(EntityDamagePackage))]
[Union(2, typeof(EntityHealthPackage))]
[Union(3, typeof(EntityInjuryPackage))]
[Union(4, typeof(EntityInjuryRequestPackage))]
public abstract record ComponentHealthPackage : IProtocolMessage
{
    public enum EventType
    {
        HitResult = 0,

        Injure = 1,

        RequestInjure = 2,

        SyncHealth = 3,

        Damage = 4 //船被破坏
    }


    public enum RequestInjureType
    {
        Unknown = 0, // 未知

        Choke = 1, // 自杀

        Cactus = 2, // 仙人掌刺伤

        Fall = 3, // 摔伤

        Fire = 4 // 着火
    }

    [IgnoreMember] public abstract EventType Type { get; }
}
