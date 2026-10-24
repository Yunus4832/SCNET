namespace Game.Protocol.Characters;

public sealed record CharacterExperiencePackage : ComponentCharacterPackage
{
    public override CharacterAction Type => CharacterAction.AddExperience;

    public int Count { get; init; }

    public bool PlaySound { get; init; }

    public float Level { get; init; }
}
