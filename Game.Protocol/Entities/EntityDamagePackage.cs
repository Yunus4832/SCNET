using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
public sealed record EntityDamagePackage : ComponentHealthPackage
{
    [IgnoreMember] public override EventType Type => EventType.Damage;

    [Key(0)] public int TargetId { get; init; }

    [Key(1)] public float Health { get; init; }
}
