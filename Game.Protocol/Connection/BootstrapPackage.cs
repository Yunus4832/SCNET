using Game.Protocol.Blocks;
using Game.Protocol.World;

using MessagePack;

using MaxLengthAttribute = System.ComponentModel.DataAnnotations.MaxLengthAttribute;

namespace Game.Protocol.Connection;

[MessagePackObject]
public sealed record BootstrapPackage : IProtocolMessage
{
    [Key(0)] public required WorldMetadata World { get; init; }

    [Key(1)] public Guid Epoch { get; init; }

    [Key(2), MaxLength(1024)] public IReadOnlyList<BlockIndexEntry> BlockMapping { get; init; } = [];

    [Key(3)]
    public ClientListPackage ClientList { get; init; } = new()
    {
        List = []
    };

    [Key(4)] public byte[] ProjectData { get; init; } = [];

    [Key(5)] public byte[] TextureData { get; init; } = [];
}
