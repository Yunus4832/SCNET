using Engine.Core.Mathematics;

namespace Game.Protocol.Terrain;

public sealed record ChunkStateResetPackage(Point2 Chunk) : IProtocolMessage
{
    public byte ID => (byte)PackageType.ChunkStateReset;
}
