using MessagePack;

namespace Game.Protocol.Blocks;

[MessagePackObject]
public sealed record EditableItemDefinitionTarget([property: Key(0)] int DefinitionId) : EditableBlockTarget;
