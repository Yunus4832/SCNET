using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
public sealed record EatRequestPackage : ComponentVitalStatPackage
{
    [IgnoreMember] public override EventType PackageEventType => EventType.RequestEat;
}
