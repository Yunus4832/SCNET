using MessagePack;

namespace Game.Protocol.Blocks;

[MessagePackObject]
public sealed record FurnitureDesignsRemovedPackage : FurniturePackage
{
    [IgnoreMember] public override EventType PackageEventType => EventType.RemoveFurnitureDesigns;

    [Key(0)] public IReadOnlyList<int> ToRemoveList { get; init; } = [];
}
