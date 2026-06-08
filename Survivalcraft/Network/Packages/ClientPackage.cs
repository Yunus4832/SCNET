using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

/// <summary>
/// 基础包模板复制
/// </summary>
public class ClientPackage : IPackage
{
    public enum EventType
    {
        Add,
        Remove,
        SyncList,
        StateChange
    }

    public EventType PackageEventType;

    public Client? Client;

    public List<Client> List = [];

    public byte ID => (byte)PackageType.Client;

    public Client? To { get; set; }

    public Client? Except { get; set; }

    public Client? From { get; set; }

    public ClientState MinNeedState => ClientState.NotConnected;

    public ClientPackage()
    {
    }

    public ClientPackage(byte id, Guid tokenId, Guid guid, string communityId, string nickname)
    {
        PackageEventType = EventType.Add;
        Client = new Client(null, id, tokenId, guid, null, communityId, nickname);
    }

    public ClientPackage(byte id)
    {
        PackageEventType = EventType.Remove;
        Client = new Client(null, id, Guid.Empty, Guid.Empty, null, string.Empty, string.Empty);
    }

    public ClientPackage(byte id, ClientState clientState)
    {
        PackageEventType = EventType.StateChange;
        Client = new Client(null, id, Guid.Empty, Guid.Empty, null, string.Empty, string.Empty)
        {
            State = clientState
        };
    }

    public ClientPackage(IEnumerable<Client> clients)
    {
        List.AddRange(clients);
        PackageEventType = EventType.SyncList;
    }


    public void ReadData(PackageStreamReader reader)
    {
        PackageEventType = reader.ReadEnum<EventType>();
        switch (PackageEventType)
        {
            case EventType.Add:
                Client = ReadItem(reader);
                break;
            case EventType.Remove:
                Client = new Client(null, reader.ReadByte(), Guid.Empty, Guid.Empty, null, string.Empty, string.Empty);
                break;
            case EventType.SyncList:
                List = [];
                var count = reader.ReadByte();
                for (var i = 0; i < count; i++)
                {
                    List.Add(ReadItem(reader));
                }

                break;
            case EventType.StateChange:
                Client = new Client(null, reader.ReadByte(), Guid.Empty, Guid.Empty, null, string.Empty, string.Empty)
                {
                    State = reader.ReadEnum<ClientState>()
                };
                break;
        }
    }

    public void WriteData(PackageStreamWriter writer)
    {
        writer.WriteEnum(PackageEventType);
        switch (PackageEventType)
        {
            case EventType.Add:
                WriteItem(writer, Client!);
                break;
            case EventType.Remove:
                writer.Write(Client!.ID);
                break;
            case EventType.SyncList:
                writer.Write((byte)List.Count);
                foreach (var c in List)
                {
                    WriteItem(writer, c);
                }

                break;
            case EventType.StateChange:
                writer.Write(Client!.ID);
                writer.WriteEnum(Client.State);
                break;
        }
    }

    public void WriteItem(PackageStreamWriter writer, Client client)
    {
        writer.Write(client.ID);
        writer.Write(client.TokenId);
        writer.Write(client.GUID);
        writer.Write(client.CommunityAccountId);
        writer.Write(client.Nickname);
    }

    public Client ReadItem(PackageStreamReader reader)
    {
        return new Client(null, reader.ReadByte(), reader.ReadGuid(), reader.ReadGuid(), null, reader.ReadString(),
            reader.ReadString());
    }
}
