using MessagePack;

namespace Game.Protocol.Blocks;

[Union(0, typeof(EditableWorldBlockTarget))]
[Union(1, typeof(EditableInventorySlotTarget))]
[Union(2, typeof(EditableItemDefinitionTarget))]
public abstract record EditableBlockTarget;
