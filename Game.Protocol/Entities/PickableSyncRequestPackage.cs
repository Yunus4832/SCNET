using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
public sealed record PickableSyncRequestPackage : PickablePackage
{
    [IgnoreMember] public override PickType Type => PickType.RequestSync;

    [Key(1)] public ushort Id { get; init; }
}
