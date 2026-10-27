using MessagePack;

namespace Game.Protocol.Characters;

[MessagePackObject]
public sealed record CharacterUsePackage : CharacterInteractionPackage
{
    [IgnoreMember] public override InteractionMessageKind InteractEvent => InteractionMessageKind.Use;
}
