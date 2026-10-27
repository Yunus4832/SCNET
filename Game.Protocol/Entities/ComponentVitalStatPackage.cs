using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
[Union(0, typeof(EatRequestPackage))]
[Union(1, typeof(VitalStatePackage))]
public abstract record ComponentVitalStatPackage : IProtocolMessage
{
    public enum EventType
    {
        SyncStat = 0,

        RequestEat = 1
    }

    [IgnoreMember] public abstract EventType PackageEventType { get; }

    [Key(0)] public int EntityId { get; init; }
}
