using MessagePack;

namespace Game.Protocol.Blocks;

[MessagePackObject]
public sealed record FurnitureSetAddDesignPackage : FurniturePackage
{
    [IgnoreMember] public override EventType PackageEventType => EventType.AddToFurnitureSet;

    [Key(0)] public string SetName { get; init; } = string.Empty;

    [Key(1)] public int FurnitureIndex { get; init; }
}
