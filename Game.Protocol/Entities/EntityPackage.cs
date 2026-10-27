using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
[Union(0, typeof(EntityRemovedPackage))]
[Union(1, typeof(EntitySnapshotPackage))]
[Union(2, typeof(EntitySyncRequestPackage))]
[Union(3, typeof(SingleEntitySnapshotPackage))]
public abstract record EntityPackage : IProtocolMessage
{
    public enum EventType : byte { Remove = 0, LoadList = 1, LoadOne = 2, RequestSync = 3 }

    [IgnoreMember] public abstract EventType Type { get; }
}
