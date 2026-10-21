using Engine.Core.Mathematics;

namespace Game.Protocol.Terrain;

public sealed record EncodedTerrainChunk(Point2 Coords, long ContentVersion, byte[] Payload);
