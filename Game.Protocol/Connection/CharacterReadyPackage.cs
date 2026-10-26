using Game.Protocol.Characters;

namespace Game.Protocol.Connection;

public sealed record CharacterReadyPackage : ComponentCharacterPackage
{
    public override CharacterAction Type => CharacterAction.IntoPlaying;
}
