namespace Game.Terrains.Distribution;

/// <summary>
///     Identifies one allocation lifetime of a chunk coordinate on a client.
/// </summary>
public readonly record struct ChunkAllocationId(Point2 Coords, ulong Generation);
