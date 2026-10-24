using Engine.Core.Mathematics;

namespace Game.Protocol.Characters;

public sealed record CharacterHitPackage : ComponentCharacterPackage
{
    public override CharacterAction Type => CharacterAction.Hit;

    public int BodyId { get; init; }

    public Vector3 HitPosition { get; init; }

    public Vector3 HitDirection { get; init; }
}
