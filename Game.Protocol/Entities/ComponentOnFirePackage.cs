namespace Game.Protocol.Entities;

public sealed record ComponentOnFirePackage : IProtocolMessage
{
    public enum EventType
    {
        ComponentOnFire,
        BlockOnFireAdd,
        BlockOnFireRemove
    }

    public int AttackerEntityId { get; init; }
    public float Duration { get; init; }
    public int EntityId { get; init; }
    public EventType Type { get; init; }
    public int X { get; init; }
    public int Y { get; init; }
    public int Z { get; init; }
    public byte ID => (byte)PackageType.ComponentOnFire;
    public ComponentOnFirePackage() { }
    public ComponentOnFirePackage(int x, int y, int z, float expandability)
    {
        Type = EventType.BlockOnFireAdd;
        X = x;
        Y = y;
        Z = z;
        Duration = expandability;
    }
    public ComponentOnFirePackage(int x, int y, int z)
    {
        Type = EventType.BlockOnFireRemove;
        X = x;
        Y = y;
        Z = z;
    }
}
