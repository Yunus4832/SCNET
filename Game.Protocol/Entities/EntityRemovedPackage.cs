using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
public sealed record EntityRemovedPackage : EntityPackage
{
    [IgnoreMember] public override EventType Type => EventType.Remove;

    [Key(0)] public int EntityId { get; init; }
}
