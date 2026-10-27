using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
[Union(0, typeof(BlockFireAddedPackage))]
[Union(1, typeof(BlockFireRemovedPackage))]
[Union(2, typeof(EntityFirePackage))]
public abstract record ComponentOnFirePackage : IProtocolMessage
{
    public enum EventType
    {
        ComponentOnFire = 0,

        BlockOnFireAdd = 1,

        BlockOnFireRemove = 2
    }

    [IgnoreMember] public abstract EventType Type { get; }
}
