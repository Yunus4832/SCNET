using Game.Protocol.Characters;
using Game.Protocol.Teams;

namespace Game.Protocol.Entities;

public sealed record CharacterDropPackage : ComponentCharacterPackage
{
    public override CharacterAction Type => CharacterAction.Drop;
}
