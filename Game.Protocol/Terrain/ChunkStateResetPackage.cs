using Engine.Core;

namespace Game.Protocol.Terrain;

public sealed record ChunkStateResetPackage(Point2 Chunk) : IProtocolMessage
{
    public byte ID => (byte)PackageType.ChunkStateReset;
}
