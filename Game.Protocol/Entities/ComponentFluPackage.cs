namespace Game.Protocol.Entities;

public sealed record ComponentFluPackage : IProtocolMessage
{
    public enum EventType
    {
        SyncStat = 0,

        FluEffect = 1,

        StartFlu = 2,

        Sneeze = 3
    }

    public int EntityId { get; init; }

    public float CoughDuration { get; init; }

    public EventType PackageEventType { get; init; }

    public float FluDuration { get; init; }

    public float FluOnset { get; init; }

    public float SneezeDuration { get; init; }

    public byte ID => (byte)PackageType.ComponentFlu;
}
