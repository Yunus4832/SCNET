using MessagePack;

namespace Game.Protocol.Characters;

[MessagePackObject]
public sealed record CharacterFlightPackage : ComponentCharacterPackage
{
    [IgnoreMember] public override CharacterAction Type => CharacterAction.CreativeFlyChange;

    [Key(1)] public bool IsCreativeFly { get; init; }
}
