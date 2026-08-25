namespace Game.Terrains.Distribution;

public readonly record struct TerrainCellDelta(
    Point3 Cell,
    int Value,
    long BaseContentVersion,
    long ResultContentVersion);
