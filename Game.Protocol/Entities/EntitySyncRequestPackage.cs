using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
public sealed record EntitySyncRequestPackage : EntityPackage
{
    [IgnoreMember] public override EventType Type => EventType.RequestSync;

    [Key(0)] public IReadOnlyList<int> EntityIdList { get; init; } = [];
}
