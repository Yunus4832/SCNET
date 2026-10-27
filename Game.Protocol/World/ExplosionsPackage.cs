using MessagePack;

namespace Game.Protocol.World;

[MessagePackObject]
[Union(0, typeof(ExplosionCellsPackage))]
[Union(1, typeof(ExplosionSoundPackage))]
public abstract record ExplosionsPackage : IProtocolMessage
{
    public enum EventType { Sound = 0, Cell = 1 }

    [IgnoreMember] public abstract EventType Type { get; }
}
