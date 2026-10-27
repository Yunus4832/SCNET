using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
public sealed record BlockFireRemovedPackage : ComponentOnFirePackage
{
    [IgnoreMember] public override EventType Type => EventType.BlockOnFireRemove;

    [Key(0)] public int X { get; init; }

    [Key(1)] public int Y { get; init; }

    [Key(2)] public int Z { get; init; }
}
