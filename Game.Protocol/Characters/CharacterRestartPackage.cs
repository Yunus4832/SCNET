namespace Game.Protocol.Characters;

public sealed record CharacterRestartPackage : ComponentCharacterPackage
{
    public override CharacterAction Type => CharacterAction.Restart;
}
