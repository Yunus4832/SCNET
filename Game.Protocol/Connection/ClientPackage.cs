namespace Game.Protocol.Connection;

public sealed record ClientPackage : IProtocolMessage
{
    public enum EventType : byte { Add = 0, Remove = 1, SyncList = 2, StateChange = 3 }

    public EventType PackageEventType { get; init; }

    public ClientIdentity? Client { get; init; }

    public IReadOnlyList<ClientIdentity> List { get; init; } = [];

    public byte ID => (byte)PackageType.Client;

    public ClientPackage()
    {
    }

    public ClientPackage(byte id, Guid tokenId, Guid guid)
    {
        PackageEventType = EventType.Add;
        Client = new ClientIdentity(id, tokenId, guid);
    }

    public ClientPackage(byte id)
    {
        PackageEventType = EventType.Remove;
        Client = new ClientIdentity(id, Guid.Empty, Guid.Empty);
    }

    public ClientPackage(byte id, ClientState state)
    {
        PackageEventType = EventType.StateChange;
        Client = new ClientIdentity(id, Guid.Empty, Guid.Empty, state);
    }

    public ClientPackage(IReadOnlyList<ClientIdentity> clients)
    {
        PackageEventType = EventType.SyncList;
        List = clients;
    }
}
