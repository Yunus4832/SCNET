using MessagePack;

namespace Game.Protocol.Blocks;

[MessagePackObject]
public sealed record FurnitureSetCreatedPackage : FurniturePackage
{
    [IgnoreMember] public override EventType PackageEventType => EventType.NewFurnitureSet;

    [Key(0)] public string Name { get; init; } = string.Empty;

    [Key(1)] public string ImportedFrom { get; init; } = string.Empty;
}
