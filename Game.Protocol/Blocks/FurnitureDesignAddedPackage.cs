using MessagePack;

namespace Game.Protocol.Blocks;

[MessagePackObject]
public sealed record FurnitureDesignAddedPackage : FurniturePackage
{
    [IgnoreMember] public override EventType PackageEventType => EventType.Add;

    [Key(0)] public int FurnitureIndex { get; init; }

    [Key(1)] public string DesignXml { get; init; } = string.Empty;
}
