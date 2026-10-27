using MessagePack;

namespace Game.Protocol.Blocks;

[MessagePackObject]
public sealed record FurnitureSetRenamedPackage : FurniturePackage
{
    [IgnoreMember] public override EventType PackageEventType => EventType.RenameFurnitureSet;

    [Key(0)] public string OldName { get; init; } = string.Empty;

    [Key(1)] public string NewName { get; init; } = string.Empty;
}
