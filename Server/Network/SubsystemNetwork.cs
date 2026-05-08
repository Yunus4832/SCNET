using EntitySystem.TemplatesDatabase;

namespace Game.Network;

public class SubsystemNetwork : Subsystem, IUpdateable
{
    public UpdateOrder UpdateOrder => UpdateOrder.Reset;

    private ITransportProvider _transport = null!;
    private readonly ChannelState[] _channelStates = new ChannelState[7];
    private ChannelFrameBuilder _frameBuilder = null!;
    private readonly List<INetworkSync> _syncRegistry = [];
    private readonly List<ReceivedChannelData> _receiveQueue = [];
    private readonly Dictionary<byte, NetworkClient> _clients = new();
    private readonly Dictionary<ushort, INetworkSync> _entitySyncMap = new();
    private double _lastFrameTime;

    public override void Load(ValuesDictionary valuesDictionary)
    {
        for (var i = 0; i < 7; i++)
        {
            _channelStates[i] = ChannelState.Create((NetworkChannel)i);
        }

        _frameBuilder = new ChannelFrameBuilder(_channelStates);
    }

    public void Update(float dt)
    {
        _lastFrameTime += dt;

        ProcessReceiveQueue();

        _frameBuilder.BeginFrame();
        CollectDirtyState();

        for (byte i = 0; i < 7; i++)
        {
            if (_channelStates[i].ShouldSend(_lastFrameTime))
            {
                var data = _frameBuilder.BuildChannel((NetworkChannel)i);
                if (data.Length > 0)
                {
                    _transport.SendToAll(data, (NetworkChannel)i);
                    _channelStates[i].MarkSent(_lastFrameTime);
                }
            }
        }
    }

    public void SetTransport(ITransportProvider transport)
    {
        _transport = transport;
        _transport.OnChannelDataReceived += (clientId, channel, data) =>
        {
            lock (_receiveQueue)
            {
                _receiveQueue.Add(new ReceivedChannelData(clientId, channel, data));
            }
        };
        _transport.OnClientConnected += OnClientConnected;
        _transport.OnClientDisconnected += OnClientDisconnected;
    }

    public void RegisterSync(INetworkSync sync)
    {
        _syncRegistry.Add(sync);
    }

    public void UnregisterSync(INetworkSync sync)
    {
        _syncRegistry.Remove(sync);
    }

    public override void OnEntityAdded(Entity entity)
    {
        foreach (var component in entity.FindComponents<Component>())
        {
            if (component is INetworkSync sync)
            {
                _syncRegistry.Add(sync);
                _entitySyncMap[entity.EntityId] = sync;
            }
        }
    }

    public override void OnEntityRemoved(Entity entity)
    {
        _entitySyncMap.Remove(entity.EntityId);
        foreach (var component in entity.FindComponents<Component>())
        {
            if (component is INetworkSync sync)
            {
                _syncRegistry.Remove(sync);
            }
        }
    }

    private void CollectDirtyState()
    {
        foreach (var sync in _syncRegistry)
        {
            if (sync.IsDirty)
            {
                ushort entityId = 0;
                if (sync is Component component && component.Entity != null)
                {
                    entityId = component.Entity.EntityId;
                }

                _frameBuilder.WriteSync(sync, entityId);
                sync.ClearDirty();
            }
        }
    }

    private void ProcessReceiveQueue()
    {
        ReceivedChannelData[] items;
        lock (_receiveQueue)
        {
            items = _receiveQueue.ToArray();
            _receiveQueue.Clear();
        }

        foreach (var item in items)
        {
            DispatchChannelData(item.ClientId, item.Channel, item.Data);
        }
    }

    private void DispatchChannelData(byte clientId, NetworkChannel channel, byte[] data)
    {
        switch (channel)
        {
            case NetworkChannel.Entity:
                ProcessEntitySync(clientId, data);
                break;
            case NetworkChannel.Input:
                ProcessInputMessage(clientId, data);
                break;
            case NetworkChannel.Control:
                ProcessControlMessage(clientId, data);
                break;
            default:
                RelayToSubsystems(clientId, channel, data);
                break;
        }
    }

    private void ProcessEntitySync(byte clientId, byte[] data)
    {
        var reader = new NetworkReader(data);
        while (reader.BytesRemaining > 0)
        {
            var entityId = reader.ReadUShort();
            if (_entitySyncMap.TryGetValue(entityId, out var sync))
            {
                ApplyEntitySync(sync, reader);
            }
            else
            {
                // Skip unknown entity data
                break;
            }
        }
    }

    private static void ApplyEntitySync(INetworkSync sync, INetworkReader reader)
    {
        sync.WriteDirtyState(new NetworkWriter());
    }

    private void ProcessInputMessage(byte clientId, byte[] data)
    {
    }

    private void ProcessControlMessage(byte clientId, byte[] data)
    {
        var reader = new NetworkReader(data);
        var msgType = reader.ReadByte();
        switch (msgType)
        {
            case 0: // Connect
                if (!_clients.ContainsKey(clientId))
                {
                    _clients[clientId] = new NetworkClient(clientId);
                }

                break;
        }
    }

    private void RelayToSubsystems(byte clientId, NetworkChannel channel, byte[] data)
    {
    }

    private void OnClientConnected(byte clientId)
    {
        _clients[clientId] = new NetworkClient(clientId);
    }

    private void OnClientDisconnected(byte clientId, string reason)
    {
        _clients.Remove(clientId);
    }
}
