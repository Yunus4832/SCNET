using Engine.Core;

namespace Game.Protocol.Players;

public sealed record PlayerInteractionPackage : ComponentPlayerPackage
{
    public override PlayerAction Type => PlayerAction.InteractEvent;

    public InteractionMessageKind InteractEvent { get; init; }

    public int PlacementRequestId { get; init; }

    public Point3 PlacementCell { get; init; }

    public int PlacementExpectedValue { get; init; }

    public Ray3 NetInteractRay { get; init; }

    public PlayerTerrainHit? NetInteractRaycast { get; init; }
}
