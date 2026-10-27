using MessagePack;

namespace Game.Protocol.Characters;

[MessagePackObject]
public sealed record CharacterCreateRequestPackage : CharacterDataPackage
{
    [IgnoreMember] public override DataType Type => DataType.Create;

    [Key(0)] public Guid CreationRequestId { get; init; }

    [Key(1)] public string CharacterName { get; init; } = string.Empty;

    [Key(2)] public string SkinName { get; init; } = string.Empty;

    [Key(3)] public byte CharacterSex { get; init; }
}
