using System.Diagnostics;
using System.Net;

using EntitySystem.Core;

using Game.Network.Enums;
using Game.Network.Packages;
using Game.Network.Serialization;

using LiteNetLib;
using LiteNetLib.Utils;

namespace Game.Network;

public class NetNode
{
    public NetworkSendStatistics SendStatistics { get; } = new();

    public int PendingPackageCount
    {
        get
        {
            lock (_pendingPackages)
            {
                return _pendingPackages.Count;
            }
        }
    }

    public enum Stage
    {
        NotConnected,
        Bootstrapping,
        Connected
    }

    private const byte _verifyByte = 0x88;

    private readonly NetManager _broadcastNetManager;

    private readonly Dictionary<Client, string> _clientsToRemove = new(ReferenceEqualityComparer.Instance);

    public readonly EventBasedNetListener Listener;

    public readonly NetManager NetManager;

    /// <summary>
    ///     包处理队列
    /// </summary>
    private readonly List<ReceivedPackage> _pendingHandlePackages = [];

    /// <summary>
    ///     包发送队列
    /// </summary>
    private readonly List<OutboundPackage> _pendingPackages = [];

    private readonly Dictionary<NetworkChannel, double> _lastPackageFlushTimes = new();

    private int _deferredSnapshotPackages;

    private double _lastSnapshotDropLogTime;

    // 只有这些客户端同意以后新客户端才能加入
    public HashSet<byte> AgreeOnPendingPeer = [];

    public bool AllowHandle = true;

    public Dictionary<int, Client> Clients = new();

    private bool _isStopping;

    public Action<Client>? OnClientStateChanged;

    public Action<Client>? OnClientTransportConnected;

    public Action<Client>? OnClientBootstrapApplied;

    public Action<Client>? OnClientBecameLive;

    public NetPeer? PendingPeer;

    public Guid TokenId;

    public Guid ConnectionEpoch { get; set; }

    public ConnectionPhase CurrentConnectionPhase { get; set; }

    public NetNode()
    {
        Listener = new EventBasedNetListener();
        NetManager = new NetManager(Listener)
        {
            MaxConnectAttempts = 6,
            DisconnectTimeout = CommonLib.DisconnectTimeout,
            UnconnectedMessagesEnabled = true,
            ChannelsCount = (byte)(Enum.GetValues<NetworkChannel>().Max(channel => (byte)channel) + 1),
            UseSafeMtu = true,
            UpdateTime = 25,
            ReuseAddress = true
        };
        var broadcastListener = new EventBasedNetListener();
        broadcastListener.NetworkReceiveUnconnectedEvent += HandleBroadcastUnconnectedEvent;
        _broadcastNetManager = new NetManager(broadcastListener)
        {
            UnconnectedMessagesEnabled = true,
            BroadcastReceiveEnabled = true
        };
    }

    public Client? Self { get; set; }

    public Client? Server { get; set; }

    public string Error { get; private set; } = string.Empty;

    public Stage CurrentStage { get; set; } = Stage.NotConnected;

    public bool IsConnected => CurrentStage == Stage.Connected;

    public bool IsServer => CurrentStage == Stage.Connected && Self == Server;

    public int ClientCount => Peers.Count() + 1;

    private Client? PendingClient => (Client?)PendingPeer?.Tag;

    public IEnumerable<Client> Peers => Clients.Values.Where(c => c != Self);

    // 当一个客户端收到一个消息
    public event Action<NetNode, IEnumerable<ReceivedPackage>>? OnReceive;

    //添加到队列
    public void QueuePackage(IPackage package, PackageAudience audience)
    {
        QueueOutboundPackage(new OutboundPackage(package, audience));
    }

    private void QueueOutboundPackage(OutboundPackage outboundPackage)
    {
        lock (_pendingPackages)
        {
            var transport = PackageTransportPolicy.Get(outboundPackage.Package);
            if (transport.Coalesce && SnapshotPackageCoalescer.TryCoalesce(_pendingPackages, outboundPackage))
            {
                return;
            }

            _pendingPackages.Add(outboundPackage);
        }
    }

    private byte FindUnusedIndex()
    {
        for (byte i = 0; i < 255; i++)
        {
            if (Clients.ContainsKey(i))
            {
                continue;
            }

            return i;
        }

        throw new Exception("服务器连接人数已满");
    }

    /// <summary>
    ///     创建客户端
    /// </summary>
    public Client CreateClient(NetPeer peer, Guid tokenId, Guid guid)
    {
        if (!IsServer)
        {
            throw new InvalidOperationException("creating client as non-server");
        }

        // 下一帧移除，否则会报错集合已更改
        foreach (var p in Peers)
        {
            if (p.GUID != guid)
            {
                continue;
            }

            RemoveClient(p, "相同的联机客户端 ID 已经连接");
        }

        var client = new Client(peer, FindUnusedIndex(), tokenId, guid, GameManager.Project);
        peer.Tag = client;
        return client;
    }

    public void AddClient(Client client)
    {
        Clients[client.ID] = client;
    }

    public void RemoveClient(Client? client, string reason = "")
    {
        if (client is null)
        {
            return;
        }

        if (_clientsToRemove.TryAdd(client, reason))
        {
            return;
        }

        if (string.IsNullOrEmpty(reason))
        {
            _clientsToRemove[client] += '\n' + reason;
        }
    }

    // 删除客户
    public void RemoveClientImmediate(Client client, string reason = "")
    {
        if (client == Self)
        {
            // 如果是自己，停止服务器
            Stop();
            return;
        }

        if (client.Peer != null && client.Peer.ConnectionState != ConnectionState.Disconnected)
        {
            if (string.IsNullOrEmpty(reason))
            {
                client.Peer.Disconnect();
            }
            else
            {
                client.Peer.Disconnect(NetDataWriter.FromString(reason));
            }
        }

        if (client == Server)
        {
            Stop();
            CurrentStage = Stage.NotConnected;
            return;
        }

        if (Clients.ContainsKey(client.ID))
        {
            if (IsServer)
            {
                QueuePackage(new ClientPackage(client.ID), PackageAudience.Global);
            }

            client.State = ClientState.NotConnected;
            OnClientStateChanged?.Invoke(client);
            Clients.Remove(client.ID);
        }
    }

    /// <summary>
    ///     服务器关闭，不执行OnClientStateChanged，防止多次Dispose
    /// </summary>
    public void RemoveAllClients(string reason = "")
    {
        var clients = new List<Client>(Clients.Values);
        foreach (var client in clients)
        {
            if (client == Self)
            {
                continue;
            }

            if (client.Peer != null && client.Peer.ConnectionState != ConnectionState.Disconnected)
            {
                if (reason != null)
                {
                    client.Peer.Disconnect(NetDataWriter.FromString(reason));
                }
                else
                {
                    client.Peer.Disconnect();
                }
            }

            Clients.Remove(client.ID);
            client.State = ClientState.NotConnected;
        }
    }

    public Client? GetClientByID(byte id)
    {
        return Clients.TryGetValue(id, out var byID) ? byID : null;
    }

    public Client? GetClientByGUID(Guid? guid, bool throwIfNull = false)
    {
        var client = Clients.Values.FirstOrDefault(c => c.GUID == guid);
        if (client is null && throwIfNull)
        {
            throw new InvalidOperationException("Client not found");
        }

        return client;
    }

    public void HandleUnconnectedEvent(IPEndPoint ip, NetPacketReader reader, UnconnectedMessageType messageType)
    {
        UnconnectedReceiveEvent(ip, reader, false);
    }

    public void HandleBroadcastUnconnectedEvent(IPEndPoint ip, NetPacketReader reader,
        UnconnectedMessageType messageType)
    {
        UnconnectedReceiveEvent(ip, reader, true);
    }

    /// <summary>
    ///     连接断开事件 服务端
    /// </summary>
    private void PeerDisconnectedEvent(NetPeer peer, DisconnectInfo disconnectInfo)
    {
        Log.Information($"{peer.EndPoint}连接关闭: " + disconnectInfo.Reason);
        if (disconnectInfo.AdditionalData != null && disconnectInfo.AdditionalData.TryGetString(out var str))
        {
            Log.Information("----Additional message----");
            Log.Information(str);
            Log.Information("----End of message----");
        }
        else
        {
            str = disconnectInfo.Reason.ToString();
        }

        if (CommonLib.WorkType == WorkType.Client)
        {
            Stop(str);
        }
        else
        {
            if (peer.Tag is not Client client)
            {
                return;
            }

            client.State = ClientState.NotConnected;
            RemoveClientImmediate(client);
            OnClientStateChanged?.Invoke(client);

            //同步Client列表
            if (peer == PendingPeer)
            {
                PendingPeer = null;
                AgreeOnPendingPeer.Clear();
            }
            else
            {
                AgreeOnPendingPeer.Remove(client.ID);
                DeliveryEvent(null, null);
            }
        }
    }

    /// <summary>
    ///     连接断开事件 客户端
    /// </summary>
    /// <param name="remoteEndPoint"></param>
    /// <param name="reader"></param>
    /// <param name="fromBroadcast"></param>
    private void UnconnectedReceiveEvent(IPEndPoint remoteEndPoint, NetPacketReader reader, bool fromBroadcast)
    {
        if (CurrentStage == Stage.NotConnected)
        {
            return;
        }

        var list = PackageManager.DecodePackages(this, reader, null, null, remoteEndPoint);
        try
        {
            foreach (var packageItem in list)
            {
                packageItem.Context.Sender?.IsLocalRemote = fromBroadcast;
                try
                {
                    PackageDispatcher.Handle(packageItem);
                }
                catch (Exception e)
                {
                    Log.Error($"[{packageItem.Package.GetType().Name}]{e.Message}");
                }
            }
        }
        catch
        {
            // ignored
        }
        finally
        {
            reader.Recycle();
        }
    }

    public void AddPendingHandlePackage(ReceivedPackage package)
    {
        Log.Debug("[排队]添加到处理队列：" + package.Package.ID);
        _pendingHandlePackages.Add(package);
    }

    /// <summary>
    ///     数据接收事件
    /// </summary>
    /// <param name="peer"></param>
    /// <param name="reader"></param>
    /// <param name="deliveryMethod"></param>
    private void NetworkReceiveEvent(NetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod)
    {
        if (CurrentStage == Stage.NotConnected)
        {
            return;
        }

        var fromClient = peer.Tag as Client;
        var list = PackageManager.DecodePackages(this, reader, peer);
        list.InsertRange(0, _pendingHandlePackages);
        _pendingHandlePackages.Clear();
        try
        {
            if (OnReceive == null)
            {
                Log.Debug("检测到有未处理的包:" + list.Count);
                _pendingHandlePackages.AddRange(list);
            }
            else
            {
                OnReceive?.Invoke(this, list);
            }
        }
        catch (Exception e)
        {
            Log.Error(e);
            RemoveClient(fromClient, "Error On ReceiveEvent:\n" + e);
        }
        finally
        {
            reader.Recycle();
        }
    }

    /// <summary>
    ///     连接被拒绝事件
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="reader"></param>
    /// <param name="type"></param>
    public void HandleConnectionReject(IPEndPoint sender, NetDataReader reader, UnconnectedMessageType type)
    {
        try
        {
            var rejectPackage = PackageManager.DecodePackage<ConnectionRejectPackage>(this, reader, null, null, sender);
            PackageDispatcher.Handle(rejectPackage.Package, rejectPackage.Context);
        }
        catch
        {
            // ignored
        }
    }

    /// <summary>
    ///     新客户端连接事件
    /// </summary>
    /// <param name="request"></param>
    private void ConnectionRequestEvent(ConnectionRequest request)
    {
        Log.Information("接收到连接请求 " + request.RemoteEndPoint);

        if (GameManager.Project == null)
        {
            SendWriterFromPackage(new ConnectionRejectPackage("请等待服务器启动完成后加入"), request, true);
        }
        else if (PendingClient != null)
        {
            Log.Information($"Too Many Request And Reject Current Request:{request.RemoteEndPoint}");
            Log.Information($"Current Pending Client:{PendingClient}");
            SendWriterFromPackage(new ConnectionRejectPackage($"当前服务器连接请求太多，请稍候再试，等待客户端回应数{AgreeOnPendingPeer.Count}"),
                request, true);
        }
        else
        {
            try
            {
                var requestPackage =
                    PackageManager.DecodePackage<ConnectionRequestPackage>(this, request.Data, null, request);
                PackageDispatcher.Handle(requestPackage.Package, requestPackage.Context);
            }
            catch (Exception e)
            {
                SendWriterFromPackage(new ConnectionRejectPackage($"错误的连接请求数据包:{e.Message}"), request, true);
                Log.Error($"ConnectionRequestEvent Error: {e}");
            }
        }
    }

    public void StartLocal()
    {
        Clients.Clear();
        Guid multiplayerClientId;
        if (RunMode.Value is RunModeType.HeadlessServer)
        {
            multiplayerClientId = Guid.NewGuid();
        }
        else
        {
            multiplayerClientId = SettingsManager.Current.MultiplayerClientId;
            if (multiplayerClientId == Guid.Empty)
            {
                throw new InvalidOperationException("The local multiplayer client id is invalid.");
            }
        }

        Self = new Client(multiplayerClientId, GameManager.Project!);
        AddClient(Self);
    }

    /// <summary>
    ///     开启服务器
    /// </summary>
    /// <param name="port"></param>
    /// <param name="broadcastPort"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public bool StartServer(int port, int? broadcastPort)
    {
        if (NetManager.IsRunning)
        {
            StopImmediate();
            throw new InvalidOperationException("已经开启服务器");
        }

        try
        {
            _isStopping = false;
            StartLocal();
            var flag = NetManager.Start(port);
            if (broadcastPort.HasValue)
            {
                flag &= _broadcastNetManager.Start(broadcastPort.Value);
            }

            if (flag)
            {
                var repositoryCount = SettingsManager.Current.ContentRepositories.Count(repository =>
                    repository.IsEnabled);
                if (repositoryCount > 0)
                {
                    Log.Information($"已配置 {repositoryCount} 个可分发内容的仓库");
                }

                Log.Information($"开启服务器成功，端口 {NetManager.LocalPort}");
                Window.Frame += Update;
                Window.Closed += StopImmediate;
                Listener.ConnectionRequestEvent += ConnectionRequestEvent;
                Listener.NetworkReceiveUnconnectedEvent += HandleUnconnectedEvent;
                Listener.NetworkReceiveEvent += NetworkReceiveEvent;
                Listener.PeerDisconnectedEvent += PeerDisconnectedEvent;
                Listener.DeliveryEvent += DeliveryEvent;
                CurrentStage = Stage.Connected;
                Server = Self;
                CommonLib.WorkType = WorkType.Server;
            }
            else
            {
                StopImmediate();
            }
        }
        catch (Exception e)
        {
            StopImmediate();
            Log.Error($"Failed to start server: {e}");
            throw;
        }

        return CurrentStage == Stage.Connected;
    }

    /// <summary>
    ///     连接服务器
    /// </summary>
    /// <param name="ep"></param>
    /// <returns></returns>
    public void ConnectServer(IPEndPoint ep)
    {
        try
        {
            _isStopping = false;
            Log.Information($"connecting to server at {ep.Address}");
            Clients.Clear();
            NetManager.Start();
            TokenId = Guid.NewGuid();
            SendWriterFromPackage(
                new ConnectionRequestPackage(
                    TokenId,
                    VersionsManager.ProtocolVersion,
                    SettingsManager.Current.MultiplayerClientId,
                    CurrentModRuntime.Value?.ModDataHash ?? ModProfileManager.EmptyDataHash
                ),
                ep,
                false
            );
            Listener.NetworkReceiveUnconnectedEvent -= HandleConnectionReject;
            Listener.NetworkReceiveEvent += NetworkReceiveEvent;
            Listener.ConnectionRequestEvent += ConnectionRequestEvent;
            Listener.NetworkReceiveUnconnectedEvent += HandleUnconnectedEvent;
            Listener.PeerDisconnectedEvent += PeerDisconnectedEvent;
            Listener.NetworkErrorEvent += (_, arg) =>
            {
                Log.Information("连接错误" + arg);
                Stop(arg.ToString());
            };
            CurrentStage = Stage.Bootstrapping;
            CommonLib.WorkType = WorkType.Client;
            Window.Frame += Update;
            Window.Closed += StopImmediate;
        }
        catch
        {
            CurrentStage = Stage.NotConnected;
        }
    }

    /// <summary>
    ///     发送回调事件
    /// </summary>
    /// <param name="peer"></param>
    /// <param name="userData"></param>
    public void DeliveryEvent(NetPeer? peer, object? userData)
    {
        if (userData is NetPeer { Tag: Client client })
        {
            AgreeOnPendingPeer.Remove(client.ID);
            Log.Debug($"Client[{client.ID}]已收到Client[{PendingClient?.ID}]加入通知");
        }

        if (AgreeOnPendingPeer.Count != 0 || PendingClient == null)
        {
            return;
        }

        AddClient(PendingClient);
        PendingClient.ConnectionEpoch = Guid.NewGuid();
        PendingClient.ConnectionPhase = ConnectionPhase.TransportConnected;
        OnClientTransportConnected?.Invoke(PendingClient);
        Log.Debug($"Client[{PendingClient.ID}]传输连接完成，开始Bootstrap");
        PendingPeer = null;
    }

    public void Stop(string error = "")
    {
        _isStopping = true;
        Error = error;
    }

    public void StopImmediate()
    {
        var wasClient = CommonLib.WorkType == WorkType.Client;
        try
        {
            PendingPeer = null;
            AgreeOnPendingPeer.Clear();
            lock (_pendingPackages)
            {
                foreach (var outboundPackage in _pendingPackages)
                {
                    outboundPackage.Fanout?.Advance(1, 0);
                }

                _pendingPackages.Clear();
            }

            _lastPackageFlushTimes.Clear();
            if (_broadcastNetManager.IsRunning)
            {
                _broadcastNetManager.Stop();
            }

            // 先发送提示，然后再关闭服务器
            if (CommonLib.WorkType == WorkType.Server)
            {
                RemoveAllClients("服务器主动关闭");
            }

            if (NetManager.IsRunning)
            {
                NetManager.Stop(true);
            }
        }
        catch (Exception e)
        {
            Log.Error(e);
        }
        finally
        {
            Window.Closed -= StopImmediate;
            Window.Frame -= Update;
            Listener.ClearConnectionRequestEvent();
            Listener.ClearNetworkReceiveUnconnectedEvent();
            Listener.ClearNetworkReceiveEvent();
            Listener.ClearPeerDisconnectedEvent();
            Listener.ClearDeliveryEvent();
            Listener.ClearPeerConnectedEvent();
            OnReceive = null;
            OnClientStateChanged = null;
            OnClientTransportConnected = null;
            OnClientBootstrapApplied = null;
            OnClientBecameLive = null;
            ConnectionEpoch = Guid.Empty;
            CurrentConnectionPhase = ConnectionPhase.TransportConnected;
            CurrentStage = Stage.NotConnected;
            CommonLib.WorkType = WorkType.Local;
            if (wasClient)
            {
                try
                {
                    GameManager.DisposeProject();
                }
                catch (Exception e)
                {
                    Log.Error(e);
                }

                ScreensManager.SwitchScreen("NetPlay");
            }

            if (!string.IsNullOrEmpty(Error))
            {
                DialogsManager.Confirm(Error, _ => { });
            }
        }
    }

    /// <summary>
    ///     开启包处理
    /// </summary>
    public void TurnOnPackageHandle(Project project)
    {
        CommonLib.Net.OnReceive += (node, list) =>
        {
            foreach (var c in list)
            {
                try
                {
                    PackageDispatcher.Handle(c);
                }
                catch (Exception e)
                {
                    Log.Error($"[{c.Package.GetType().Name}]{e.Message}");
                }
            }
        };
    }

    public void SendWriterFromPackage(IPackage package, IPEndPoint endPoint, bool isSend = true)
    {
        SendWriterFromPackages([package], null, null, endPoint, isSend);
    }

    public void SendWriterFromPackage(IPackage package, NetPeer? netPeer, bool useDeliveryEvent = false)
    {
        SendWriterFromPackages([package], netPeer, null, null, useDeliveryEvent);
    }

    /// <summary>
    ///     发送一个数据包到还未连接的远程
    /// </summary>
    /// <param name="package">数据包</param>
    /// <param name="request">远程连接请求</param>
    /// <param name="reject">发送后是否拒绝连接 true拒绝 false不拒绝</param>
    public void SendWriterFromPackage(IPackage package, ConnectionRequest request, bool reject)
    {
        if (reject)
        {
            request.Reject();
        }

        SendWriterFromPackages([package], null, request);
    }

    /// <summary>
    ///     发送多个数据包
    /// </summary>
    /// <param name="packages"></param>
    /// <param name="netPeer"></param>
    /// <param name="request"></param>
    /// <param name="iPEndPoint"></param>
    /// <param name="useDeliveryEvent">true 使用delivery事件回调 true 发送消息 false 发送连接消息</param>
    public void SendWriterFromPackages(
        IEnumerable<IPackage> packages,
        NetPeer? netPeer = null,
        ConnectionRequest? request = null,
        IPEndPoint? iPEndPoint = null,
        bool useDeliveryEvent = false
    )
    {
        var packageList = packages as IList<IPackage> ?? packages.ToList();
        if (packageList.Count == 0)
        {
            return;
        }

        var measurements = new List<(Type PackageType, int PayloadBytes)>();
        var transport = PackageTransportPolicy.Get(packageList[0]);
        var w = CreatePackageWriter(packageList, transport, out _, measurements);
        if (netPeer != null)
        {
            transport = useDeliveryEvent
                ? PackageTransportPolicy.Control
                : transport;
            if (!useDeliveryEvent)
            {
                netPeer.Send(w, transport.ChannelNumber, transport.DeliveryMethod);
            }
            else
            {
                netPeer.SendWithDeliveryEvent(w, transport.ChannelNumber, transport.DeliveryMethod, netPeer);
            }

            SendStatistics.Record(transport.Channel, w.Length, measurements);
            foreach (var package in packageList)
            {
                SendStatistics.RecordFanout(package.GetType(), transport.Channel, 1);
            }
        }
        else if (request != null)
        {
            NetManager.SendUnconnectedMessage(w, request.RemoteEndPoint);
        }
        else if (iPEndPoint != null)
        {
            if (!useDeliveryEvent)
            {
                NetManager.Connect(iPEndPoint, w);
            }
            else
            {
                NetManager.SendUnconnectedMessage(w, iPEndPoint);
            }
        }
    }

    public static int SendUnconnectedPackage(NetManager netManager, IPackage package, IPEndPoint endPoint)
    {
        ArgumentNullException.ThrowIfNull(endPoint);
        var writer = CreateUnconnectedWriter(package, out var size);
        netManager.SendUnconnectedMessage(writer, endPoint);
        return size;
    }

    public static void BroadcastServerDiscovery(NetManager netManager)
    {
        var writer = CreateUnconnectedWriter(new ServerInfoPackage(true), out _);
        netManager.SendBroadcast(writer, SettingsManager.Current.BroadcastPort);
    }

    private static NetDataWriter CreateUnconnectedWriter(IPackage package, out int size)
    {
        using var writer = new PackageStreamWriter { IsServer = false };
        writer.Write(_verifyByte);
        writer.Write(package.ID);
        package.WriteData(writer);
        return CommonLib.GetWriter(writer, out size);
    }

    public void Update()
    {
        // 先处理UI操作，Inventory序号增加后再处理客户端过来的包
        // 发送物品同步数据
        SubsystemInventories.FlushSyncItems();

        foreach (var pair in _clientsToRemove)
        {
            RemoveClientImmediate(pair.Key, pair.Value);
        }

        _clientsToRemove.Clear();
        // 批处理发送包队列，按传输通道分流，避免可靠大包阻塞实时状态。
        lock (_pendingPackages)
        {
            if (_pendingPackages.Count > 0)
            {
                FlushPendingPackages();
            }
        }

        if (_isStopping)
        {
            StopImmediate();
        }
        else
        {
            NetManager.PollEvents();
            if (_broadcastNetManager.IsRunning)
            {
                _broadcastNetManager.PollEvents();
            }
        }
    }

    private void FlushPendingPackages()
    {
        List<OutboundPackage> packages;
        var deferredSnapshots = new HashSet<OutboundPackage>();
        var flushedChannels = new HashSet<NetworkChannel>();
        var now = Time.RealTime;
        lock (_pendingPackages)
        {
            packages = DequeueFlushablePackages(now, flushedChannels);
        }

        if (packages.Count == 0)
        {
            return;
        }

        for (var i = 0; i < packages.Count; i++)
        {
            var outboundPackage = packages[i];
            packages[i] = outboundPackage with
            {
                Fanout = outboundPackage.Fanout ?? new NetworkMessageFanout(SendStatistics,
                    outboundPackage.Package.GetType(), PackageTransportPolicy.Get(outboundPackage.Package).Channel)
            };
        }

        if (CommonLib.WorkType == WorkType.Client)
        {
            if (Clients.Count > 0)
            {
                SendPendingPackagesToClient(Clients[0], packages, false, deferredSnapshots);
            }
        }
        else
        {
            foreach (var client in Clients.Values.Where(client => client.IsConnected))
            {
                SendPendingPackagesToClient(client, packages, true, deferredSnapshots);
            }
        }

        var continuations = new Dictionary<NetworkMessageFanout, (int Consumed, int Deferred)>();
        foreach (var outboundPackage in packages)
        {
            var fanout = outboundPackage.Fanout!;
            continuations.TryGetValue(fanout, out var counts);
            continuations[fanout] = (counts.Consumed + 1, counts.Deferred);
        }

        foreach (var outboundPackage in deferredSnapshots)
        {
            var fanout = outboundPackage.Fanout!;
            var counts = continuations[fanout];
            continuations[fanout] = (counts.Consumed, counts.Deferred + 1);
        }

        foreach (var (fanout, counts) in continuations)
        {
            fanout.Advance(counts.Consumed, counts.Deferred);
        }

        foreach (var outboundPackage in deferredSnapshots)
        {
            QueueOutboundPackage(outboundPackage);
        }

        foreach (var channel in flushedChannels)
        {
            _lastPackageFlushTimes[channel] = now;
        }
    }

    private List<OutboundPackage> DequeueFlushablePackages(double now,
        HashSet<NetworkChannel> flushedChannels)
    {
        var packages = new List<OutboundPackage>();
        for (var i = _pendingPackages.Count - 1; i >= 0; i--)
        {
            var outboundPackage = _pendingPackages[i];
            var transport = PackageTransportPolicy.Get(outboundPackage.Package);
            if (!ShouldFlush(transport, now))
            {
                continue;
            }

            packages.Add(outboundPackage);
            flushedChannels.Add(transport.Channel);
            _pendingPackages.RemoveAt(i);
        }

        packages.Reverse();
        return packages;
    }

    private bool ShouldFlush(PackageTransport transport, double now)
    {
        if (!_lastPackageFlushTimes.TryGetValue(transport.Channel, out var lastFlushTime))
        {
            return true;
        }

        return now - lastFlushTime >= transport.FlushInterval;
    }

    private void SendPendingPackagesToClient(
        Client client,
        List<OutboundPackage> packages,
        bool checkClientState,
        HashSet<OutboundPackage> deferredSnapshots)
    {
        if (client.Peer == null)
        {
            return;
        }

        foreach (var channel in Enum.GetValues<NetworkChannel>())
        {
            var routingStarted = Stopwatch.GetTimestamp();
            PackageTransport? transport = null;
            var channelPackages = new List<OutboundPackage>();

            foreach (var outboundPackage in packages)
            {
                var package = outboundPackage.Package;
                var currentTransport = PackageTransportPolicy.Get(package);
                if (currentTransport.Channel != channel)
                {
                    continue;
                }

                if (!CanSendPackageToClient(outboundPackage, client, checkClientState))
                {
                    continue;
                }

                transport = currentTransport;
                channelPackages.Add(outboundPackage);
            }

            SendStatistics.RecordRouting(packages.Count, channelPackages.Count,
                Stopwatch.GetTimestamp() - routingStarted);

            if (channelPackages.Count == 0 || transport == null)
            {
                continue;
            }

            foreach (var outboundPackage in SendPackageBatches(client, channelPackages, transport.Value))
            {
                deferredSnapshots.Add(outboundPackage with { Audience = PackageAudience.To(client) });
            }
        }
    }

    private List<OutboundPackage> SendPackageBatches(
        Client client,
        List<OutboundPackage> packages,
        PackageTransport transport)
    {
        var peer = client.Peer!;
        if (packages.Any(outboundPackage => IsTerrainChunkPackage(outboundPackage.Package)))
        {
            foreach (var outboundPackage in packages)
            {
                var measurements = new List<(Type PackageType, int PayloadBytes)>();
                var chunkWriter = CreatePackageWriter([outboundPackage.Package], transport, out _, measurements);
                peer.Send(chunkWriter, transport.ChannelNumber, transport.DeliveryMethod);
                SendStatistics.Record(transport.Channel, chunkWriter.Length, measurements);
                outboundPackage.Fanout!.DeliveredTo(client);
            }

            return [];
        }

        var maxPacketSize = peer.GetMaxSinglePacketSize(transport.DeliveryMethod);
        var remainingPackages = new List<OutboundPackage>(packages.Count);
        foreach (var outboundPackage in packages)
        {
            if (outboundPackage.Package is SubsystemBodyPackage
                {
                    PackageEventType: SubsystemBodyPackage.EventType.BodyUpdate
                } bodyPackage)
            {
                CreatePackageWriter([bodyPackage], transport, out var bodyPackageSize);
                if (bodyPackageSize > maxPacketSize)
                {
                    var chunks = SplitOversizedBodyPackage(bodyPackage, transport, maxPacketSize);
                    foreach (var chunk in chunks)
                    {
                        var measurements = new List<(Type PackageType, int PayloadBytes)>();
                        var chunkWriter = CreatePackageWriter([chunk], transport, out _, measurements);
                        peer.Send(chunkWriter, transport.ChannelNumber, transport.DeliveryMethod);
                        SendStatistics.Record(transport.Channel, chunkWriter.Length, measurements);
                    }

                    if (chunks.Count > 0)
                    {
                        outboundPackage.Fanout!.DeliveredTo(client);
                    }

                    continue;
                }
            }

            remainingPackages.Add(outboundPackage);
        }

        if (remainingPackages.Count == 0)
        {
            return [];
        }

        var remainingMeasurements = new List<(Type PackageType, int PayloadBytes)>();
        var writer = CreatePackageWriter(remainingPackages.Select(item => item.Package), transport, out var packetSize,
            remainingMeasurements);
        if (packetSize <= maxPacketSize || CanFragment(transport.DeliveryMethod))
        {
            peer.Send(writer, transport.ChannelNumber, transport.DeliveryMethod);
            SendStatistics.Record(transport.Channel, writer.Length, remainingMeasurements);
            foreach (var outboundPackage in remainingPackages)
            {
                outboundPackage.Fanout!.DeliveredTo(client);
            }
            return [];
        }

        if (transport.Coalesce)
        {
            return SendBudgetedSnapshot(client, remainingPackages, transport, maxPacketSize);
        }

        var batch = new List<OutboundPackage>();

        foreach (var package in remainingPackages)
        {
            batch.Add(package);
            CreatePackageWriter(batch.Select(item => item.Package), transport, out packetSize);
            if (packetSize <= maxPacketSize || batch.Count == 1)
            {
                continue;
            }

            batch.RemoveAt(batch.Count - 1);
            SendPackageBatch(client, batch, transport, maxPacketSize);
            batch.Clear();
            batch.Add(package);
        }

        SendPackageBatch(client, batch, transport, maxPacketSize);
        return [];
    }

    private List<OutboundPackage> SendBudgetedSnapshot(
        Client client,
        List<OutboundPackage> packages,
        PackageTransport transport,
        int maxPacketSize)
    {
        var peer = client.Peer!;
        var deferredPackages = new List<OutboundPackage>();
        var selectedPackages = new List<OutboundPackage>();
        var startIndex = Time.FrameIndex % packages.Count;
        for (var offset = 0; offset < packages.Count; offset++)
        {
            var package = packages[(startIndex + offset) % packages.Count];
            selectedPackages.Add(package);
            CreatePackageWriter(selectedPackages.Select(item => item.Package), transport, out var packetSize);
            if (packetSize <= maxPacketSize)
            {
                continue;
            }

            selectedPackages.RemoveAt(selectedPackages.Count - 1);
            deferredPackages.Add(package);
            RecordDeferredSnapshot(package.Package, packetSize, maxPacketSize);
        }

        if (selectedPackages.Count == 0)
        {
            return deferredPackages;
        }

        var measurements = new List<(Type PackageType, int PayloadBytes)>();
        var writer = CreatePackageWriter(selectedPackages.Select(item => item.Package), transport, out _, measurements);
        peer.Send(writer, transport.ChannelNumber, transport.DeliveryMethod);
        SendStatistics.Record(transport.Channel, writer.Length, measurements);
        foreach (var outboundPackage in selectedPackages)
        {
            outboundPackage.Fanout!.DeliveredTo(client);
        }
        return deferredPackages;
    }

    private List<SubsystemBodyPackage> SplitOversizedBodyPackage(
        SubsystemBodyPackage bodyPackage,
        PackageTransport transport,
        int maxPacketSize)
    {
        var chunks = new List<SubsystemBodyPackage>();
        var chunk = new SubsystemBodyPackage
        {
            PackageEventType = SubsystemBodyPackage.EventType.BodyUpdate
        };

        foreach (var item in bodyPackage.BodyList)
        {
            chunk.BodyList.Add(item);
            CreatePackageWriter([chunk], transport, out var chunkSize);
            if (chunkSize <= maxPacketSize)
            {
                continue;
            }

            chunk.BodyList.RemoveAt(chunk.BodyList.Count - 1);
            chunks.Add(chunk);
            chunk = new SubsystemBodyPackage
            {
                PackageEventType = SubsystemBodyPackage.EventType.BodyUpdate
            };
            chunk.BodyList.Add(item);
        }

        if (chunk.BodyList.Count > 0)
        {
            chunks.Add(chunk);
        }

        return chunks;
    }

    private void SendPackageBatch(
        Client client,
        List<OutboundPackage> packages,
        PackageTransport transport,
        int maxPacketSize)
    {
        if (packages.Count == 0)
        {
            return;
        }

        var measurements = new List<(Type PackageType, int PayloadBytes)>();
        var peer = client.Peer!;
        var writer = CreatePackageWriter(packages.Select(item => item.Package), transport, out var packetSize, measurements);
        if (packetSize > maxPacketSize)
        {
            Log.Error(
                $"Dropping oversized {transport.DeliveryMethod} package batch " +
                $"({packetSize}/{maxPacketSize} bytes): {string.Join(", ", packages.Select(p => p.Package.GetType().Name))}");
            return;
        }

        peer.Send(writer, transport.ChannelNumber, transport.DeliveryMethod);
        SendStatistics.Record(transport.Channel, writer.Length, measurements);
        foreach (var outboundPackage in packages)
        {
            outboundPackage.Fanout!.DeliveredTo(client);
        }
    }

    private NetDataWriter CreatePackageWriter(
        IEnumerable<IPackage> packages,
        PackageTransport transport,
        out int packetSize,
        List<(Type PackageType, int PayloadBytes)>? measurements = null)
    {
        var packageList = packages as IReadOnlyCollection<IPackage> ?? packages.ToArray();
        var serializationStarted = Stopwatch.GetTimestamp();
        using var writer = new PackageStreamWriter
        {
            IsServer = IsServer
        };
        foreach (var package in packageList)
        {
            writer.Write(_verifyByte);
            writer.Write(package.ID);
            var start = writer.BaseStream.Position;
            package.WriteData(writer);
            measurements?.Add((package.GetType(), checked((int)(writer.BaseStream.Position - start))));
        }

        var serializationFinished = Stopwatch.GetTimestamp();
        var netWriter = CommonLib.GetWriter(
            writer,
            out var compressedSize,
            GetCompressionPolicy(packageList, transport));
        packetSize = compressedSize + sizeof(int);
        SendStatistics.RecordEncoding(packageList.Count, writer.BaseStream.Length, netWriter.Length,
            serializationFinished - serializationStarted,
            Stopwatch.GetTimestamp() - serializationFinished);
        return netWriter;
    }

    private static CommonLib.CompressionPolicy GetCompressionPolicy(
        IEnumerable<IPackage> packages,
        PackageTransport transport)
    {
        if (transport.Mode != TransportMode.Reliable)
        {
            return CommonLib.CompressionPolicy.None;
        }

        // Chunk payloads are independently compressed and cached by the terrain codec.
        return packages.Any(IsTerrainChunkPackage)
            ? CommonLib.CompressionPolicy.None
            : CommonLib.CompressionPolicy.Adaptive;
    }

    private static bool IsTerrainChunkPackage(IPackage package) =>
        package is SubsystemTerrainPackage
        {
            Type: SubsystemTerrainPackage.DataType.SyncTerrainChunkFragment
        };

    private static bool CanFragment(DeliveryMethod deliveryMethod)
    {
        return deliveryMethod is DeliveryMethod.ReliableOrdered or DeliveryMethod.ReliableUnordered;
    }

    private void RecordDeferredSnapshot(IPackage package, int packetSize, int maxPacketSize)
    {
        _deferredSnapshotPackages++;
        var now = Time.RealTime;
        if (_lastSnapshotDropLogTime > 0.0 && now - _lastSnapshotDropLogTime < 5.0)
        {
            return;
        }

        Log.Warning(
            $"Deferred {_deferredSnapshotPackages} snapshot package(s) due to MTU budget. " +
            $"Last: {package.GetType().Name} ({packetSize}/{maxPacketSize} bytes).");
        _deferredSnapshotPackages = 0;
        _lastSnapshotDropLogTime = now;
    }

    private static bool CanSendPackageToClient(OutboundPackage outboundPackage, Client client,
        bool checkClientState)
    {
        if (!outboundPackage.Audience.Includes(client))
        {
            return false;
        }

        var package = outboundPackage.Package;

        if (!checkClientState)
        {
            return true;
        }

        if (package is BootstrapPackage or InitialWorldSnapshotPackage)
        {
            return true;
        }

        return client.ConnectionPhase == ConnectionPhase.Live && client.State >= package.MinNeedState;
    }
}
