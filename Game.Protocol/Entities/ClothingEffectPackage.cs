using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
public sealed record ClothingEffectPackage : ComponentBehaviorPackage
{
    [IgnoreMember] public override EventType PackageEventType => EventType.ClothingEffect;

    [Key(1)] public ClothingEffectKind Kind { get; init; }

    [Key(2)] public int ClothingValue { get; init; }
}
