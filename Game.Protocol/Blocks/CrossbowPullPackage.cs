using MessagePack;

namespace Game.Protocol.Blocks;

[MessagePackObject]
public sealed record CrossbowPullPackage : BlockEditPackage
{
    [IgnoreMember] public override EventType Type => EventType.CrossbowPull;

    [Key(0)] public int InventoryId { get; init; }

    [Key(1)] public int SlotIndex { get; init; }
}
