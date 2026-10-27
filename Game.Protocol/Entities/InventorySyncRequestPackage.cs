using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
public sealed record InventorySyncRequestPackage : ComponentInventoryPackage
{
    [IgnoreMember] public override EventType PackageEventType => EventType.QueryErrorInventoryInfo;

    [Key(0)] public int InventoryID { get; init; }
}
