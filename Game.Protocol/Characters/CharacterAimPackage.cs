using Engine.Core.Mathematics;

namespace Game.Protocol.Characters;

public sealed record CharacterAimPackage : ComponentCharacterPackage
{
    public override CharacterAction Type => CharacterAction.AimEvent;

    public SyncAction AimEvent { get; init; }

    public Ray3? NetAimRay { get; init; }
}
