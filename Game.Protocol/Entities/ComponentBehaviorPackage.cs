namespace Game.Protocol.Entities;

public sealed record ComponentBehaviorPackage : IProtocolMessage
{
    public enum EventType
    {
        ChaseBehavior,
        RandomPeck,
        RandomFeed,
        EatPickable,
        HumanRow,
        CreatureSound,
        DigInMud,
        FishOutOfWater
    }

    public EventType PackageEventType { get; init; }
    public int EntityId { get; init; }
    public bool RowLeft { get; init; }
    public bool RowRight { get; init; }
    public byte Type { get; init; }
    public byte ID => (byte)PackageType.ComponentBehavior;
}
