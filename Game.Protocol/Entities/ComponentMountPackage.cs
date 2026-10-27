using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
[Union(0, typeof(DismountedPackage))]
[Union(1, typeof(DismountRequestPackage))]
[Union(2, typeof(MountedPackage))]
[Union(3, typeof(MountRequestPackage))]
public abstract record ComponentMountPackage : IProtocolMessage
{
    public enum EventType
    {
        Mount = 0,

        Dismount = 1,

        MountRequest = 2,

        DismountRequest = 3
    }

    [IgnoreMember] public abstract EventType Type { get; }

    [Key(0)] public int FromId { get; init; }
}
