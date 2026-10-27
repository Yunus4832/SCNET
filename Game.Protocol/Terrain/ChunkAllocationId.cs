using Engine.Core.Mathematics;

using MessagePack;

namespace Game.Protocol.Terrain;

/// <summary>
///     Identifies one allocation lifetime of a chunk coordinate on a client.
/// </summary>
[MessagePackObject]
public readonly record struct ChunkAllocationId([property: Key(0)] Point2 Coords, [property: Key(1)] ulong Generation);
