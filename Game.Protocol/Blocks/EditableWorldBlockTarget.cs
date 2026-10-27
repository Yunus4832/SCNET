using Game.Protocol.Terrain;

using MessagePack;

namespace Game.Protocol.Blocks;

[MessagePackObject]
public sealed record EditableWorldBlockTarget([property: Key(0)] CellFace CellFace) : EditableBlockTarget;
