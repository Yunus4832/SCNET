namespace Game.Protocol.Entities;

public sealed record ComponentVitalStatPackage : IProtocolMessage
{
    public enum EventType
    {
        SyncStat,
        RequestEat
    }

    public float Food { get; init; }
    public int EntityId { get; init; }
    public EventType PackageEventType { get; init; }
    public float Sleep { get; init; }
    public float Stamina { get; init; }
    public float Temperature { get; init; }
    public float Wetness { get; init; }
    public byte ID => (byte)PackageType.ComponentVitalStat;
}
