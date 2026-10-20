using Game.Protocol.Blocks;
using Game.Protocol.World;

namespace Game.Protocol.Connection;

public sealed record BootstrapPackage : IProtocolMessage
{
    public required WorldMetadata World { get; init; }
    public Guid Epoch { get; init; }
    public IReadOnlyList<BlockIndexEntry> BlockMapping { get; init; } = [];
    public ClientPackage ClientList { get; init; } = new([]);
    public byte[] ProjectData { get; init; } = [];
    public byte[] TextureData { get; init; } = [];
    public byte ID => (byte)PackageType.Bootstrap;
}
