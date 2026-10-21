namespace Game.Protocol.Entities;

public sealed record ComponentSleepPackage : IProtocolMessage
{
    public enum EventType
    {
        SleepRequest = 0,

        Sleep = 1,

        WakeupRequest = 2,

        WakeUp = 3
    }

    public bool AllowManualWakeup { get; init; }

    public int EntityId { get; init; }

    public bool Result { get; init; }

    public EventType Type { get; init; }

    public string Reason { get; init; } = string.Empty;

    public byte ID => (byte)PackageType.ComponentSleep;

    public ComponentSleepPackage()
    {
    }

    public ComponentSleepPackage(
        int entityId,
        EventType eventType,
        bool allow = false,
        bool result = false,
        string msg = ""
    )
    {
        Reason = msg;
        EntityId = entityId;
        Type = eventType;
        AllowManualWakeup = allow;
        Result = result;
    }
}
