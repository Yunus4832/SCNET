using Engine.Core;

namespace Game.Protocol.Players;

public sealed record PlayerHitPackage : ComponentPlayerPackage
{
    public override PlayerAction Type => PlayerAction.Hit;

    public int BodyId { get; init; }

    public Vector3 HitPosition { get; init; }

    public Vector3 HitDirection { get; init; }
}
