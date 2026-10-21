namespace Game.Protocol.Entities;

public sealed record ComponentBehaviorPackage : IProtocolMessage
{
    public enum EventType
    {
        ChaseBehavior = 0,

        RandomPeck = 1,

        RandomFeed = 2,

        EatPickable = 3,

        HumanRow = 4,

        CreatureSound = 5,

        DigInMud = 6,

        FishOutOfWater = 7
    }

    public EventType PackageEventType { get; init; }

    public int EntityId { get; init; }

    public bool RowLeft { get; init; }

    public bool RowRight { get; init; }

    public byte Type { get; init; }

    public byte ID => (byte)PackageType.ComponentBehavior;
}
