using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
[Union(2, typeof(EntityFirePackage))]
[Union(3, typeof(BlockBurnEffectPackage))]
[Union(4, typeof(BlockFuseAddedPackage))]
[Union(5, typeof(BlockFuseRemovedPackage))]
public abstract record ComponentOnFirePackage : IProtocolMessage
{
    public enum EventType
    {
        ComponentOnFire = 0,

        BlockBurnEffect = 3,

        FuseAdded = 4,

        FuseRemoved = 5
    }

    [IgnoreMember] public abstract EventType Type { get; }
}
