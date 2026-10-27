using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
public sealed record PickablesRemovedPackage : PickablePackage
{
    [IgnoreMember] public override PickType Type => PickType.DeleteList;

    [Key(1)] public IReadOnlyList<ushort> RemovedIds { get; init; } = [];
}
