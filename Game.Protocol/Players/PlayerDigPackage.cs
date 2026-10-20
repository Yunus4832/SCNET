using Engine.Core;

namespace Game.Protocol.Players;

public sealed record PlayerDigPackage : ComponentPlayerPackage
{
    public override PlayerAction Type => PlayerAction.DigEvent;
    public SyncAction DigEvent { get; init; }
    public int DigRequestId { get; init; }
    public int DigExpectedValue { get; init; }
    public Ray3? NetDigRay { get; init; }
    public PlayerTerrainHit? NetDigRaycast { get; init; }
}
