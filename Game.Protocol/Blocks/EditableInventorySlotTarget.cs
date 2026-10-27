using MessagePack;

namespace Game.Protocol.Blocks;

[MessagePackObject]
public sealed record EditableInventorySlotTarget(
    [property: Key(0)] int InventoryId,
    [property: Key(1)] int SlotIndex
) : EditableBlockTarget;
