using Engine.Core.Mathematics;

using MessagePack;

namespace Game.Protocol.Terrain;

/// <summary>标识客户端同一区块坐标的一次分配周期。</summary>
[MessagePackObject]
public readonly record struct ChunkAllocationId([property: Key(0)] Point2 Coords, [property: Key(1)] ulong Generation);
