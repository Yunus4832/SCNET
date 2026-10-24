using Engine.Core.Mathematics;

namespace Game.Protocol.Characters;

public sealed record CharacterDigPackage : ComponentCharacterPackage
{
    public override CharacterAction Type => CharacterAction.DigEvent;

    public SyncAction DigEvent { get; init; }

    public int DigRequestId { get; init; }

    public int DigExpectedValue { get; init; }

    public Ray3? NetDigRay { get; init; }

    public CharacterTerrainHit? NetDigRaycast { get; init; }
}
