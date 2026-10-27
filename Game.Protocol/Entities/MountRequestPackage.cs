using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
public sealed record MountRequestPackage : ComponentMountPackage
{
    [IgnoreMember] public override EventType Type => EventType.MountRequest;

    [Key(1)] public int TargetId { get; init; }
}
