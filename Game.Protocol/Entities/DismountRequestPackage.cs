using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
public sealed record DismountRequestPackage : ComponentMountPackage
{
    [IgnoreMember] public override EventType Type => EventType.DismountRequest;
}
