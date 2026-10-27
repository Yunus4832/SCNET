using Engine.Core.Mathematics;

using MessagePack;

namespace Game.Protocol.Characters;

[MessagePackObject]
public sealed record CharacterHitPackage : ComponentCharacterPackage
{
    [IgnoreMember] public override CharacterAction Type => CharacterAction.Hit;

    [Key(1)] public int BodyId { get; init; }

    [Key(2)] public Vector3 HitPosition { get; init; }

    [Key(3)] public Vector3 HitDirection { get; init; }
}
