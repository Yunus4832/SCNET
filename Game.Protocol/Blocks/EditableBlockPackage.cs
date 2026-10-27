using MessagePack;

namespace Game.Protocol.Blocks;

[MessagePackObject]
[Union(0, typeof(EditableBlockDataPackage))]
[Union(1, typeof(EditableBlockValuePackage))]
public abstract record EditableBlockPackage : IProtocolMessage
{
    [Key(0)] public required EditableBlockTarget Target { get; init; }

    [Key(1)] public BuiltInBlockEditKind ItemType { get; init; }
}
