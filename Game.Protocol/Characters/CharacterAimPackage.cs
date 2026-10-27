using Engine.Core.Mathematics;

using MessagePack;

namespace Game.Protocol.Characters;

[MessagePackObject]
public sealed record CharacterAimPackage : ComponentCharacterPackage
{
    [IgnoreMember] public override CharacterAction Type => CharacterAction.AimEvent;

    [Key(1)] public SyncAction AimEvent { get; init; }

    [Key(2)] public Ray3? NetAimRay { get; init; }
}
