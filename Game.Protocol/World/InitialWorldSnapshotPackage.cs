using Game.Protocol.Connection;
using Game.Protocol.Players;

namespace Game.Protocol.World;

public sealed record InitialWorldSnapshotPackage : IProtocolMessage
{
    public Guid Epoch { get; init; }
    public ClientPackage ClientList { get; init; } = new([]);
    public IReadOnlyList<byte[]> Players { get; init; } = [];
    public byte[] EntityData { get; init; } = [];
    public IReadOnlyList<PlayerEntitySnapshot> PlayerEntities { get; init; } = [];
    public byte ID => (byte)PackageType.InitialWorldSnapshot;
}
