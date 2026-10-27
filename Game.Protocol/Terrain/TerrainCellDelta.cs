using Engine.Core.Mathematics;

using MessagePack;

namespace Game.Protocol.Terrain;

[MessagePackObject]
public readonly record struct TerrainCellDelta(
    [property: Key(0)] Point3 Cell,
    [property: Key(1)] int Value,
    [property: Key(2)] long BaseContentVersion,
    [property: Key(3)] long ResultContentVersion
);
