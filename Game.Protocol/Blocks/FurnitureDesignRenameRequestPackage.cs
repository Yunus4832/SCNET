using Game.Protocol.Terrain;

using MessagePack;

namespace Game.Protocol.Blocks;

[MessagePackObject]
public sealed record FurnitureDesignRenameRequestPackage : FurniturePackage
{
    [IgnoreMember] public override EventType PackageEventType => EventType.RenameDesignRequest;

    [Key(0)] public CellFace Source { get; init; }

    [Key(1)] public int FurnitureIndex { get; init; }

    [Key(2)] public string Name { get; init; } = string.Empty;
}
