using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
public sealed record PickablePositionsPackage : PickablePackage
{
    [IgnoreMember] public override PickType Type => PickType.Update;

    [Key(1)] public IReadOnlyList<PickablePosition> Positions { get; init; } = [];
}
