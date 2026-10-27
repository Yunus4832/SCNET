using Game.Protocol.Characters;

using MessagePack;

namespace Game.Protocol.Connection;

[MessagePackObject]
public sealed record CharacterReadyPackage : ComponentCharacterPackage
{
    [IgnoreMember] public override CharacterAction Type => CharacterAction.IntoPlaying;
}
