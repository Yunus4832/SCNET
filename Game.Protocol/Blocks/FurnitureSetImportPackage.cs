using MessagePack;

namespace Game.Protocol.Blocks;

[MessagePackObject]
public sealed record FurnitureSetImportPackage : FurniturePackage
{
    [IgnoreMember] public override EventType PackageEventType => EventType.ImportFurnitureSet;

    [Key(0)] public string DesignXml { get; init; } = string.Empty;

    [Key(1)] public string Name { get; init; } = string.Empty;
}
