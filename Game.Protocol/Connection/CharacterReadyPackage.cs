using Game.Protocol.Characters;
using Game.Protocol.Teams;

namespace Game.Protocol.Connection;

public sealed record CharacterReadyPackage : ComponentCharacterPackage
{
    public override CharacterAction Type => CharacterAction.IntoPlaying;
}
