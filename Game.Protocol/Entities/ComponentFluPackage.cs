using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
public sealed record ComponentFluPackage : IProtocolMessage
{
    public enum EventType
    {
        SyncStat = 0,

        FluEffect = 1,

        StartFlu = 2,

        Sneeze = 3
    }

    [Key(0)] public int EntityId { get; init; }

    [Key(1)] public float CoughDuration { get; init; }

    [Key(2)] public EventType PackageEventType { get; init; }

    [Key(3)] public float FluDuration { get; init; }

    [Key(4)] public float FluOnset { get; init; }

    [Key(5)] public float SneezeDuration { get; init; }
}
