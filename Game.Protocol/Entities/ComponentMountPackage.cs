namespace Game.Protocol.Entities;

public sealed record ComponentMountPackage : IProtocolMessage
{
    public enum EventType
    {
        Mount,
        Dismount,
        MountRequest,
        DismountRequest
    }

    public int FromId { get; init; }
    public int TargetId { get; init; }
    public EventType Type { get; init; }
    public byte ID => (byte)PackageType.ComponentMount;
    public ComponentMountPackage()
    {
    }
    public ComponentMountPackage(int fromId, int targetId, bool isRequest = false)
    {
        Type = isRequest ? EventType.MountRequest : EventType.Mount;
        FromId = fromId;
        TargetId = targetId;
    }
    public ComponentMountPackage(int fromId, bool isRequest = false)
    {
        Type = isRequest ? EventType.DismountRequest : EventType.Dismount;
        FromId = fromId;
    }
}
