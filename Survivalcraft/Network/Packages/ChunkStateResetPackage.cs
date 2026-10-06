using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public sealed class ChunkStateResetPackage : IPackage
{
    public Point2 Chunk;
    public byte ID => (byte)PackageType.ChunkStateReset;
    public ClientState MinNeedState => ClientState.Playing;

    public void WriteData(PackageStreamWriter writer)
    {
        writer.Write(Chunk);
    }

    public void ReadData(PackageStreamReader reader)
    {
        Chunk = reader.ReadPoint2();
    }
}
