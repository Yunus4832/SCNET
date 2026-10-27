using MessagePack;

namespace Game.Protocol.Connection;

[MessagePackObject]
[Union(0, typeof(ClientAddedPackage))]
[Union(1, typeof(ClientListPackage))]
[Union(2, typeof(ClientRemovedPackage))]
[Union(3, typeof(ClientStateChangedPackage))]
public abstract record ClientPackage : IProtocolMessage
{
    public enum EventType : byte { Add = 0, Remove = 1, SyncList = 2, StateChange = 3 }

    [IgnoreMember] public abstract EventType PackageEventType { get; }
}
