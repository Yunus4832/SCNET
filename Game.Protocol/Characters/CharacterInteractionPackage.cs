using Engine.Core.Mathematics;

namespace Game.Protocol.Characters;

public sealed record CharacterInteractionPackage : ComponentCharacterPackage
{
    public override CharacterAction Type => CharacterAction.InteractEvent;

    public InteractionMessageKind InteractEvent { get; init; }

    public int PlacementRequestId { get; init; }

    public Point3 PlacementCell { get; init; }

    public int PlacementExpectedValue { get; init; }

    public Ray3 NetInteractRay { get; init; }

    public CharacterTerrainHit? NetInteractRaycast { get; init; }
}
