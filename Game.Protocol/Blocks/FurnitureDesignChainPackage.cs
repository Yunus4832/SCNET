using MessagePack;

namespace Game.Protocol.Blocks;

[MessagePackObject]
public sealed record FurnitureDesignChainPackage : FurniturePackage
{
    [IgnoreMember] public override EventType PackageEventType => EventType.DesignChain;

    [Key(0)] public string DesignXml { get; init; } = string.Empty;
}
