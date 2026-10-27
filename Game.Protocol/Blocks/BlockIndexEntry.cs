using MessagePack;

namespace Game.Protocol.Blocks;

[MessagePackObject]
public readonly record struct BlockIndexEntry([property: Key(0)] ResourceId Id, [property: Key(1)] ushort Index);
