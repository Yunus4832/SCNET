using Game.Protocol.Characters;
using Game.Protocol.Connection;

using MessagePack;

using MaxLengthAttribute = System.ComponentModel.DataAnnotations.MaxLengthAttribute;

namespace Game.Protocol.World;

[MessagePackObject]
public sealed record InitialWorldSnapshotPackage : IProtocolMessage
{
    [Key(0)] public Guid Epoch { get; init; }

    [Key(1)]
    public ClientListPackage ClientList { get; init; } = new()
    {
        List = []
    };

    [Key(2)] public IReadOnlyList<byte[]> Characters { get; init; } = [];

    [Key(3)] public byte[] EntityData { get; init; } = [];

    [Key(4), MaxLength(1024)] public IReadOnlyList<CharacterEntitySnapshot> CharacterEntities { get; init; } = [];
}
