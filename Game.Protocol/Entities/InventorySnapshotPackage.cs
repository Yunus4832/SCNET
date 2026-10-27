using System.Collections.ObjectModel;

using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
public sealed record InventorySnapshotPackage : ComponentInventoryPackage
{
    [IgnoreMember] public override EventType PackageEventType => EventType.InventorySync;

    [Key(0)]
    public IReadOnlyDictionary<int, IReadOnlyList<InventorySlotSnapshot>> Slots { get; init; } =
        ReadOnlyDictionary<int, IReadOnlyList<InventorySlotSnapshot>>.Empty;
}
