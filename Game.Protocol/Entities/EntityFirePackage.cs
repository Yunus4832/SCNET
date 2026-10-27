using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
public sealed record EntityFirePackage : ComponentOnFirePackage
{
    [IgnoreMember] public override EventType Type => EventType.ComponentOnFire;

    [Key(0)] public int EntityId { get; init; }

    [Key(1)] public int AttackerEntityId { get; init; }

    [Key(2)] public float Duration { get; init; }
}
