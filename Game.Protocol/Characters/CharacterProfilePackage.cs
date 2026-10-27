using MessagePack;

namespace Game.Protocol.Characters;

[MessagePackObject]
public sealed record CharacterProfilePackage : CharacterDataPackage
{
    [IgnoreMember] public override DataType Type => DataType.Modify;

    [Key(0)] public Guid CharacterId { get; init; }

    [Key(1)] public string CharacterName { get; init; } = string.Empty;

    [Key(2)] public string SkinName { get; init; } = string.Empty;

    [Key(3)] public byte CharacterSex { get; init; }
}
