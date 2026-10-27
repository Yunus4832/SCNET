using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
public sealed record BlockFireAddedPackage : ComponentOnFirePackage
{
    [IgnoreMember] public override EventType Type => EventType.BlockOnFireAdd;

    [Key(0)] public int X { get; init; }

    [Key(1)] public int Y { get; init; }

    [Key(2)] public int Z { get; init; }

    [Key(3)] public float Duration { get; init; }
}
