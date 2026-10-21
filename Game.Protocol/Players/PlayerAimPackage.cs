using Engine.Core.Mathematics;

namespace Game.Protocol.Players;

public sealed record PlayerAimPackage : ComponentPlayerPackage
{
    public override PlayerAction Type => PlayerAction.AimEvent;

    public SyncAction AimEvent { get; init; }

    public Ray3? NetAimRay { get; init; }
}
