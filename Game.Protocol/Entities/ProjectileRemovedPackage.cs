using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
public sealed record ProjectileRemovedPackage : ProjectilePackage
{
    [IgnoreMember] public override EventType Type => EventType.Remove;
}
