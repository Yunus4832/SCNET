using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
public sealed record PickableRemovedPackage : PickablePackage
{
    [IgnoreMember] public override PickType Type => PickType.Delete;

    [Key(1)] public ushort Id { get; init; }

    [Key(2)] public bool PlaySound { get; init; }
}
