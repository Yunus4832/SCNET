namespace Game.NetWork.Packages;

/// <summary>
/// 基础包模板复制
/// </summary>
public class ClientPackage : IPackage
{
    private enum EventType
    {
        Add,
        Remove,
        SyncList,
        StateChange
    }

    private EventType _eventType;

    private Client? _client;

    private List<Client> _list = [];

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
        _eventType = EventType.Add;
        _client = new Client(null, id, tokenId, guid, null, communityId, nickname);
    }

    public ClientPackage(byte id)
    {
        _eventType = EventType.Remove;
        _client = new Client(null, id, Guid.Empty, Guid.Empty, null, string.Empty, string.Empty);
    }

    public ClientPackage(byte id, ClientState clientState)
    {
        _eventType = EventType.StateChange;
        _client = new Client(null, id, Guid.Empty, Guid.Empty, null, string.Empty, string.Empty)
        {
            State = clientState
        };
    }

    public ClientPackage(IEnumerable<Client> clients)
    {
        _list.AddRange(clients);
        _eventType = EventType.SyncList;
    }


    public void Handle(ProjectNet? projectNet, NetNode netNode, bool isServer)
    {
        switch (_eventType)
        {
            case EventType.Add:
                netNode.AddClient(new Client(From?.Peer, _client!.ID, _client.TokenId, _client.GUID, projectNet,
                    _client.CommunityAccountId, _client.Nickname));
#if DEBUG
                Log.Information("AddClient:" + _client.ID);
#endif
                break;
            case EventType.Remove:
#if DEBUG
                Log.Information("RemoveClient:" + _client!.ID);
#endif
                if (netNode.Clients.ContainsKey(_client!.ID))
                {
                    var client = netNode.Clients[_client.ID];
                    client.State = ClientState.NotConnected;
                    netNode.OnClientStateChanged?.Invoke(client);
                    netNode.Clients.Remove(_client.ID);
                }

                break;
            case EventType.SyncList:
                foreach (var c in _list)
                {
                    if (c.ID == 0)
                    {
                        c.Peer = From?.Peer;
                        c.Peer?.Tag = c;
                        netNode.Server = From;
                    }
                    else
                    {
                        if (c.TokenId == CommonLib.Net.TokenId)
                        {
                            netNode.Self = c;
                        }
                    }

                    netNode.AddClient(c);
                }
#if DEBUG
                Log.Information("SyncList End:" + _list.Count);
#endif
                if (netNode.Self == null)
                {
                    throw new Exception("Cannot find Self In Client List");
                }

                netNode.CurrentStage = NetNode.Stage.Connected;
                break;
            case EventType.StateChange:
                if (netNode.Clients.TryGetValue(_client!.ID, out var nodeClient))
                {
                    From = nodeClient;
                    if (From.State != _client.State)
                    {
                        From.State = _client.State;
                        netNode.OnClientStateChanged?.Invoke(From);
                    }
                }

                break;
        }
    }

    public void ReadData(PackageStreamReader reader)
    {
        _eventType = reader.ReadEnum<EventType>();
        switch (_eventType)
        {
            case EventType.Add:
                _client = ReadItem(reader);
                break;
            case EventType.Remove:
                _client = new Client(null, reader.ReadByte(), Guid.Empty, Guid.Empty, null, string.Empty, string.Empty);
                break;
            case EventType.SyncList:
                _list = [];
                var count = reader.ReadByte();
                for (var i = 0; i < count; i++)
                {
                    _list.Add(ReadItem(reader));
                }

                break;
            case EventType.StateChange:
                _client = new Client(null, reader.ReadByte(), Guid.Empty, Guid.Empty, null, string.Empty, string.Empty)
                {
                    State = reader.ReadEnum<ClientState>()
                };
                break;
        }
    }

    public void WriteData(PackageStreamWriter writer)
    {
        writer.WriteEnum(_eventType);
        switch (_eventType)
        {
            case EventType.Add:
                WriteItem(writer, _client!);
                break;
            case EventType.Remove:
                writer.Write(_client!.ID);
                break;
            case EventType.SyncList:
                writer.Write((byte)_list.Count);
                foreach (var c in _list)
                {
                    WriteItem(writer, c);
                }

                break;
            case EventType.StateChange:
                writer.Write(_client!.ID);
                writer.WriteEnum(_client.State);
                break;
        }
    }

    private void WriteItem(PackageStreamWriter writer, Client client)
    {
        writer.Write(client.ID);
        writer.Write(client.TokenId);
        writer.Write(client.GUID);
        writer.Write(client.CommunityAccountId);
        writer.Write(client.Nickname);
    }

    private Client ReadItem(PackageStreamReader reader)
    {
        return new Client(null, reader.ReadByte(), reader.ReadGuid(), reader.ReadGuid(), null, reader.ReadString(),
            reader.ReadString());
    }
}
