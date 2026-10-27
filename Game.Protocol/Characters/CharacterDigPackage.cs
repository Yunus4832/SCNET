using Engine.Core.Mathematics;

using MessagePack;

namespace Game.Protocol.Characters;

[MessagePackObject]
public sealed record CharacterDigPackage : ComponentCharacterPackage
{
    [IgnoreMember] public override CharacterAction Type => CharacterAction.DigEvent;

    [Key(1)] public SyncAction DigEvent { get; init; }

    [Key(2)] public int DigRequestId { get; init; }

    [Key(3)] public int DigExpectedValue { get; init; }

    [Key(4)] public Ray3? NetDigRay { get; init; }

    [Key(5)] public CharacterTerrainHit? NetDigRaycast { get; init; }
}
