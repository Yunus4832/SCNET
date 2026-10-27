using MessagePack;

namespace Game.Protocol.Blocks;

[MessagePackObject]
public sealed record MovingBlockAddedPackage : MovingBlockPackage
{
    [IgnoreMember] public override EventType Type => EventType.Add;

    [Key(2)] public byte[]? AddData { get; init; }
}
