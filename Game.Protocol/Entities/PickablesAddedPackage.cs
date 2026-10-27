using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
public sealed record PickablesAddedPackage : PickablePackage
{
    [IgnoreMember] public override PickType Type => PickType.CreateList;

    [Key(1)] public IReadOnlyList<PickableSnapshot> Pickables { get; init; } = [];
}
