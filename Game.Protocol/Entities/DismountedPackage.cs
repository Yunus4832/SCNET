using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
public sealed record DismountedPackage : ComponentMountPackage
{
    [IgnoreMember] public override EventType Type => EventType.Dismount;
}
