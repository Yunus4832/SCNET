using MessagePack;

namespace Game.Protocol.Characters;

[MessagePackObject]
public sealed record CharacterCreateRejectedPackage : CharacterDataPackage
{
    [IgnoreMember] public override DataType Type => DataType.CreationRejected;

    [Key(0)] public Guid CreationRequestId { get; init; }

    [Key(1)] public CharacterCreationError CreationError { get; init; }
}
