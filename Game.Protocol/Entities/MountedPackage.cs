using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
public sealed record MountedPackage : ComponentMountPackage
{
    [IgnoreMember] public override EventType Type => EventType.Mount;

    [Key(1)] public int TargetId { get; init; }
}
