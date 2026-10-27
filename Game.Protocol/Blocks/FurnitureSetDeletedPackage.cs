using MessagePack;

namespace Game.Protocol.Blocks;

[MessagePackObject]
public sealed record FurnitureSetDeletedPackage : FurniturePackage
{
    [IgnoreMember] public override EventType PackageEventType => EventType.DeleteFurnitureSet;

    [Key(0)] public string Name { get; init; } = string.Empty;
}
