using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
[Union(0, typeof(PickableAddedPackage))]
[Union(1, typeof(PickablePositionsPackage))]
[Union(2, typeof(PickableRemovedPackage))]
[Union(3, typeof(PickablesAddedPackage))]
[Union(4, typeof(PickablesRemovedPackage))]
[Union(5, typeof(PickableSyncRequestPackage))]
public abstract record PickablePackage : IProtocolMessage
{
    public const int MaxPositionsPerSnapshot = 24;

    public enum PickType { Create = 0, Update = 1, Delete = 2, RequestSync = 3, CreateList = 4, DeleteList = 5 }

    [IgnoreMember] public abstract PickType Type { get; }

    [Key(0)] public uint StateTick { get; init; }
}
