using Engine.Core.Mathematics;

using MessagePack;

namespace Game.Protocol.Terrain;

[MessagePackObject]
public sealed record ChunkStateResetPackage([property: Key(0)] Point2 Chunk) : IProtocolMessage
{
}
