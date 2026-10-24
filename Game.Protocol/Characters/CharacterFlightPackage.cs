namespace Game.Protocol.Characters;

public sealed record CharacterFlightPackage : ComponentCharacterPackage
{
    public override CharacterAction Type => CharacterAction.CreativeFlyChange;

    public bool IsCreativeFly { get; init; }
}
