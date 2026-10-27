using Engine.Core.Mathematics;

using MessagePack;

namespace Game.Protocol.Terrain;

[MessagePackObject]
public sealed record EncodedTerrainChunk(
    [property: Key(0)] Point2 Coords,
    [property: Key(1)] long ContentVersion,
    [property: Key(2)] byte[] Payload
);
