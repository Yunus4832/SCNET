using Game.Protocol.Characters;

namespace Game.Protocol.Entities;

public sealed record CharacterDropPackage : ComponentCharacterPackage
{
    public override CharacterAction Type => CharacterAction.Drop;
}
