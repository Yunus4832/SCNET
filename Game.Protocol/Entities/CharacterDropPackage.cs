using Game.Protocol.Characters;

using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
public sealed record CharacterDropPackage : ComponentCharacterPackage
{
    [IgnoreMember] public override CharacterAction Type => CharacterAction.Drop;
}
