using MessagePack;

namespace Game.Protocol.Blocks;

[MessagePackObject]
public sealed record OpenInventoryPackage : BlockEditPackage
{
    [IgnoreMember] public override EventType Type => EventType.OpenInventoryByID;

    [Key(0)] public int InventoryId { get; init; }
}
