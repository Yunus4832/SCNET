using Engine.Core.Mathematics;

namespace Game.Protocol.Terrain;

public readonly record struct TerrainCellDelta(
    Point3 Cell,
    int Value,
    long BaseContentVersion,
    long ResultContentVersion
);
