using MessagePack;

namespace Game.Protocol.Characters;

[MessagePackObject]
public sealed record CharacterRestartPackage : ComponentCharacterPackage
{
    [IgnoreMember] public override CharacterAction Type => CharacterAction.Restart;
}
