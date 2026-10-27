using Engine.Core.Mathematics;

using MessagePack;

namespace Game.Protocol.Characters;

[MessagePackObject]
[Union(0, typeof(CharacterPlacePackage))]
[Union(1, typeof(CharacterUsePackage))]
public abstract record CharacterInteractionPackage : ComponentCharacterPackage
{
    [IgnoreMember] public abstract InteractionMessageKind InteractEvent { get; }

    [IgnoreMember] public override CharacterAction Type => CharacterAction.InteractEvent;

    [Key(1)] public Ray3 NetInteractRay { get; init; }

    [Key(2)] public CharacterTerrainHit? NetInteractRaycast { get; init; }
}
