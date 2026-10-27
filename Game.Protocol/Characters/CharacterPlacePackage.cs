using Engine.Core.Mathematics;

using MessagePack;

namespace Game.Protocol.Characters;

[MessagePackObject]
public sealed record CharacterPlacePackage : CharacterInteractionPackage
{
    [IgnoreMember] public override InteractionMessageKind InteractEvent => InteractionMessageKind.Place;

    [Key(3)] public int PlacementRequestId { get; init; }

    [Key(4)] public Point3 PlacementCell { get; init; }

    [Key(5)] public int PlacementExpectedValue { get; init; }
}
