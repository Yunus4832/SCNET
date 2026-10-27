using Game.Protocol.Characters;

using MessagePack;

using MaxLengthAttribute = System.ComponentModel.DataAnnotations.MaxLengthAttribute;

namespace Game.Protocol.Entities;

[MessagePackObject]
public sealed record SingleEntitySnapshotPackage : EntityPackage
{
    [IgnoreMember] public override EventType Type => EventType.LoadOne;

    [Key(0)] public byte[] EntityData { get; init; } = [];

    [Key(1)] public IReadOnlyList<CharacterSnapshot> Characters { get; init; } = [];

    [Key(2), MaxLength(1024)] public IReadOnlyList<CharacterEntitySnapshot> CharacterEntities { get; init; } = [];
}
