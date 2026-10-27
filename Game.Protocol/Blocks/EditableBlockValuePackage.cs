using MessagePack;

namespace Game.Protocol.Blocks;

[MessagePackObject]
public sealed record EditableBlockValuePackage : EditableBlockPackage
{
    [Key(2)] public int Value { get; init; }
}
