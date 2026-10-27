using MessagePack;

namespace Game.Protocol.Characters;

[MessagePackObject]
public sealed record CharacterExperiencePackage : ComponentCharacterPackage
{
    [IgnoreMember] public override CharacterAction Type => CharacterAction.AddExperience;

    [Key(1)] public int Count { get; init; }

    [Key(2)] public bool PlaySound { get; init; }

    [Key(3)] public float Level { get; init; }
}
