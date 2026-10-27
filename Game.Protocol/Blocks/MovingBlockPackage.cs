using MessagePack;

namespace Game.Protocol.Blocks;

[MessagePackObject]
[Union(0, typeof(MovingBlockAddedPackage))]
[Union(1, typeof(MovingBlockRemovedPackage))]
[Union(2, typeof(MovingBlockStatePackage))]
[Union(3, typeof(MovingBlockStoppedPackage))]
[Union(4, typeof(PistonSoundPackage))]
public abstract record MovingBlockPackage : IProtocolMessage
{
    public enum EventType
    {
        Add = 0,

        Remove = 1,

        Stopped = 2,

        Update = 3,

        PistonSound = 4
    }

    [IgnoreMember] public abstract EventType Type { get; }

    [Key(0)] public int NetworkId { get; init; }

    [Key(1)] public uint StateTick { get; init; }
}
