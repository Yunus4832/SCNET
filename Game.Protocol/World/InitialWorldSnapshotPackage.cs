using Game.Protocol.Characters;
using Game.Protocol.Connection;
using Game.Protocol.Teams;

namespace Game.Protocol.World;

public sealed record InitialWorldSnapshotPackage : IProtocolMessage
{
    public Guid Epoch { get; init; }

    public ClientPackage ClientList { get; init; } = new([]);

    public IReadOnlyList<byte[]> Characters { get; init; } = [];

    public byte[] EntityData { get; init; } = [];

    public IReadOnlyList<CharacterEntitySnapshot> CharacterEntities { get; init; } = [];

    public byte ID => (byte)PackageType.InitialWorldSnapshot;
}
