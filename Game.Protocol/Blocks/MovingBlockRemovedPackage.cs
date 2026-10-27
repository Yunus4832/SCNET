using MessagePack;

namespace Game.Protocol.Blocks;

[MessagePackObject]
public sealed record MovingBlockRemovedPackage : MovingBlockPackage
{
    [IgnoreMember] public override EventType Type => EventType.Remove;
}
