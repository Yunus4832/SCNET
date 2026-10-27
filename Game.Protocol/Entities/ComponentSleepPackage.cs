using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
[Union(0, typeof(SleepRequestPackage))]
[Union(1, typeof(SleepResultPackage))]
[Union(2, typeof(WakeupPackage))]
[Union(3, typeof(WakeupRequestPackage))]
public abstract record ComponentSleepPackage : IProtocolMessage
{
    public enum EventType
    {
        SleepRequest = 0,

        Sleep = 1,

        WakeupRequest = 2,

        WakeUp = 3
    }

    [IgnoreMember] public abstract EventType Type { get; }

    [Key(0)] public int EntityId { get; init; }
}
