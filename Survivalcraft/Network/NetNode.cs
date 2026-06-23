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
    public enum Stage
    {
        NotConnected,
        WaitForClientList,
        Connected
    }

    private const byte _verifyByte = 0x88;

    private readonly NetManager _broadcastNetManager;

    private readonly Dictionary<Client, string> _clientsToRemove = new();

    public readonly EventBasedNetListener Listener;

    public readonly NetManager NetManager;

    /// <summary>
    /// 包处理队列
    /// </summary>
    private readonly List<IPackage> _pendingHandlePackages = [];

    /// <summary>
    /// 包发送队列
    /// </summary>
    private readonly List<IPackage> _pendingPackages = [];

    private readonly Dictionary<NetworkChannel, double> _lastPackageFlushTimes = new();

    private int _deferredSnapshotPackages;

    private double _lastSnapshotDropLogTime;

    // 只有这些客户端同意以后新客户端才能加入
    public HashSet<byte> AgreeOnPendingPeer = [];

    public bool AllowHandle = true;

    public Dictionary<int, Client> Clients = new();

    private bool _isStopping;

    public Action<Client>? OnClientStateChanged;

    public NetPeer? PendingPeer;

    public Guid TokenId;

    public NetNode()
    {
        Listener = new EventBasedNetListener();
        NetManager = new NetManager(Listener)
        {
            MaxConnectAttempts = 6,
            DisconnectTimeout = CommonLib.DisconnectTimeout,
            UnconnectedMessagesEnabled = true,
            ChannelsCount = 8,
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
    public event Action<NetNode, IEnumerable<IPackage>>? OnReceive;

    //添加到队列
    public void QueuePackage(IPackage package)
    {
        lock (_pendingPackages)
        {
            var transport = PackageTransportPolicy.Get(package);
            if (transport.Coalesce && SnapshotPackageCoalescer.TryCoalesce(_pendingPackages, package))
            {
                return;
            }

            _pendingPackages.Add(package);
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
    /// 创建客户端
    /// </summary>
    public Client CreateClient(NetPeer peer, Guid tokenId, Guid guid, string dataId, string nickname)
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

            RemoveClient(p, "账号从其它设备登录");
        }

        var client = new Client(peer, FindUnusedIndex(), tokenId, guid, GameManager.Project, dataId, nickname);
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
                QueuePackage(new ClientPackage(client.ID));
            }

            client.State = ClientState.NotConnected;
            OnClientStateChanged?.Invoke(client);
            Clients.Remove(client.ID);
        }
    }

    /// <summary>
    /// 服务器关闭，不执行OnClientStateChanged，防止多次Dispose
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
    /// 连接断开事件 服务端
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
    /// 连接断开事件 客户端
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
                packageItem.From?.IsLocalRemote = fromBroadcast;
                try
                {
                    PackageDispatcher.Handle(packageItem, this, IsServer);
                }
                catch (Exception e)
                {
                    Log.Error($"[{packageItem.GetType().Name}]{e.Message}");
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

    public void AddPendingHandlePackage(IPackage package)
    {
        Log.Debug("[排队]添加到处理队列：" + package.ID);
        _pendingHandlePackages.Add(package);
    }

    /// <summary>
    /// 数据接收事件
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
    /// 连接被拒绝事件
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="reader"></param>
    /// <param name="type"></param>
    public void HandleConnectionReject(IPEndPoint sender, NetDataReader reader, UnconnectedMessageType type)
    {
        try
        {
            var rejectPackage = PackageManager.DecodePackage<ConnectionRejectPackage>(this, reader, null, null, sender);
            PackageDispatcher.Handle(rejectPackage, this, IsServer);
        }
        catch
        {
            // ignored
        }
    }

    /// <summary>
    /// 新客户端连接事件
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
                PackageDispatcher.Handle(requestPackage, this, true);
            }
            catch (Exception e)
            {
                SendWriterFromPackage(new ConnectionRejectPackage($"错误的连接请求数据包:{e.Message}"), request, true);
                Log.Information("ConnectionRequestEvent Error:" + e.StackTrace);
            }
        }
    }

    public void StartLocal()
    {
        Clients.Clear();
        var localGuid = RunMode.Value is RunModeType.HeadlessServer
            ? Guid.NewGuid()
            : new Guid(SettingsManager.OnlineAccessToken);
        Self = new Client(localGuid, GameManager.Project!);
        AddClient(Self);
    }

    /// <summary>
    /// 开启服务器
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
                if (!string.IsNullOrEmpty(SettingsManager.ModServerAddress))
                {
                    Log.Information($"模组服务器已被指定为: {SettingsManager.ModServerAddress}");
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
                CurrentStage = Stage.NotConnected;
                Stop();
            }
        }
        catch (Exception e)
        {
            Log.Error(e.Message);
        }

        return CurrentStage == Stage.Connected;
    }

    /// <summary>
    /// 连接服务器
    /// </summary>
    /// <param name="ep"></param>
    /// <param name="passwd"></param>
    /// <returns></returns>
    public void ConnectServer(IPEndPoint ep, string passwd = "")
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
                    SettingsManager.CommunityAccessUser,
                    SettingsManager.OnlineAccessToken,
                    passwd,
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
            CurrentStage = Stage.WaitForClientList;
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
    /// 发送回调事件
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
        if (PendingClient.State != ClientState.Connected)
        {
            PendingClient.State = ClientState.Connected;
            OnClientStateChanged?.Invoke(PendingClient);
        }

        QueuePackage(new ClientPackage(Clients.Values) { To = PendingClient });
        Log.Debug($"Client[{PendingClient.ID}]已完成加入过程");
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
    /// 开启包处理
    /// </summary>
    public void TurnOnPackageHandle(Project project)
    {
        CommonLib.Net.OnReceive += (node, list) =>
        {
            foreach (var c in list)
            {
                try
                {
                    PackageDispatcher.Handle(c, node, IsServer);
                }
                catch (Exception e)
                {
                    Log.Error($"[{c.GetType().Name}]{e.Message}");
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
    /// 发送一个数据包到还未连接的远程
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
    /// 发送多个数据包
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

        var writer = new PackageStreamWriter();
        writer.IsServer = IsServer;
        foreach (var package in packageList)
        {
            writer.Write(_verifyByte);
            writer.Write(package.ID);
            package.WriteData(writer);
        }

        var w = CommonLib.GetWriter(writer, out _);
        if (netPeer != null)
        {
            var transport = useDeliveryEvent
                ? PackageTransportPolicy.Control
                : PackageTransportPolicy.Get(packageList[0]);
            if (!useDeliveryEvent)
            {
                netPeer.Send(w, transport.ChannelNumber, transport.DeliveryMethod);
            }
            else
            {
                netPeer.SendWithDeliveryEvent(w, transport.ChannelNumber, transport.DeliveryMethod, netPeer);
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

    public static int SendWriterFromPackage(
        NetManager netManager,
        IEnumerable<IPackage> packages,
        IPEndPoint? endPoint
    )
    {
        var writer = new PackageStreamWriter();
        writer.IsServer = false;
        foreach (var packet in packages)
        {
            writer.Write(_verifyByte);
            writer.Write(packet.ID);
            packet.WriteData(writer);
        }

        var w = CommonLib.GetWriter(writer, out var size);
        if (endPoint != null)
        {
            netManager.SendUnconnectedMessage(w, endPoint);
        }
        else
        {
            netManager.SendBroadcast(w, SettingsManager.BroadcastPort);
        }

        return size;
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
        List<IPackage> packages;
        var deferredSnapshots = new HashSet<IPackage>();
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

        foreach (var package in deferredSnapshots)
        {
            QueuePackage(package);
        }

        foreach (var channel in flushedChannels)
        {
            _lastPackageFlushTimes[channel] = now;
        }
    }

    private List<IPackage> DequeueFlushablePackages(double now, HashSet<NetworkChannel> flushedChannels)
    {
        var packages = new List<IPackage>();
        for (var i = _pendingPackages.Count - 1; i >= 0; i--)
        {
            var package = _pendingPackages[i];
            var transport = PackageTransportPolicy.Get(package);
            if (!ShouldFlush(transport, now))
            {
                continue;
            }

            packages.Add(package);
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
        List<IPackage> packages,
        bool checkClientState,
        HashSet<IPackage> deferredSnapshots)
    {
        if (client.Peer == null)
        {
            return;
        }

        foreach (var channel in Enum.GetValues<NetworkChannel>())
        {
            PackageTransport? transport = null;
            var channelPackages = new List<IPackage>();

            foreach (var package in packages)
            {
                var currentTransport = PackageTransportPolicy.Get(package);
                if (currentTransport.Channel != channel)
                {
                    continue;
                }

                if (!CanSendPackageToClient(package, client, checkClientState))
                {
                    continue;
                }

                transport = currentTransport;
                channelPackages.Add(package);
            }

            if (channelPackages.Count == 0 || transport == null)
            {
                continue;
            }

            foreach (var package in SendPackageBatches(client.Peer, channelPackages, transport.Value))
            {
                deferredSnapshots.Add(package);
            }
        }
    }

    private List<IPackage> SendPackageBatches(
        NetPeer peer,
        List<IPackage> packages,
        PackageTransport transport)
    {
        var writer = CreatePackageWriter(packages, out var packetSize);
        var maxPacketSize = peer.GetMaxSinglePacketSize(transport.DeliveryMethod);
        if (packetSize <= maxPacketSize || CanFragment(transport.DeliveryMethod))
        {
            peer.Send(writer, transport.ChannelNumber, transport.DeliveryMethod);
            return [];
        }

        if (transport.Coalesce)
        {
            return SendBudgetedSnapshot(peer, packages, transport, maxPacketSize);
        }

        var batch = new List<IPackage>();

        foreach (var package in packages)
        {
            batch.Add(package);
            CreatePackageWriter(batch, out packetSize);
            if (packetSize <= maxPacketSize || batch.Count == 1)
            {
                continue;
            }

            batch.RemoveAt(batch.Count - 1);
            SendPackageBatch(peer, batch, transport, maxPacketSize);
            batch.Clear();
            batch.Add(package);
        }

        SendPackageBatch(peer, batch, transport, maxPacketSize);
        return [];
    }

    private List<IPackage> SendBudgetedSnapshot(
        NetPeer peer,
        List<IPackage> packages,
        PackageTransport transport,
        int maxPacketSize)
    {
        var deferredPackages = new List<IPackage>();
        var selectedPackages = new List<IPackage>();
        var startIndex = Time.FrameIndex % packages.Count;
        for (var offset = 0; offset < packages.Count; offset++)
        {
            var package = packages[(startIndex + offset) % packages.Count];
            selectedPackages.Add(package);
            CreatePackageWriter(selectedPackages, out var packetSize);
            if (packetSize <= maxPacketSize)
            {
                continue;
            }

            selectedPackages.RemoveAt(selectedPackages.Count - 1);
            deferredPackages.Add(package);
            RecordDeferredSnapshot(package, packetSize, maxPacketSize);
        }

        if (selectedPackages.Count == 0)
        {
            return deferredPackages;
        }

        var writer = CreatePackageWriter(selectedPackages, out _);
        peer.Send(writer, transport.ChannelNumber, transport.DeliveryMethod);
        return deferredPackages;
    }

    private void SendPackageBatch(
        NetPeer peer,
        List<IPackage> packages,
        PackageTransport transport,
        int maxPacketSize)
    {
        if (packages.Count == 0)
        {
            return;
        }

        var writer = CreatePackageWriter(packages, out var packetSize);
        if (packetSize > maxPacketSize)
        {
            Log.Error(
                $"Dropping oversized {transport.DeliveryMethod} package batch " +
                $"({packetSize}/{maxPacketSize} bytes): {string.Join(", ", packages.Select(p => p.GetType().Name))}");
            return;
        }

        peer.Send(writer, transport.ChannelNumber, transport.DeliveryMethod);
    }

    private NetDataWriter CreatePackageWriter(IEnumerable<IPackage> packages, out int packetSize)
    {
        var writer = new PackageStreamWriter
        {
            IsServer = IsServer
        };
        foreach (var package in packages)
        {
            writer.Write(_verifyByte);
            writer.Write(package.ID);
            package.WriteData(writer);
        }

        var netWriter = CommonLib.GetWriter(writer, out var compressedSize);
        packetSize = compressedSize + sizeof(int);
        return netWriter;
    }

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

    private static bool CanSendPackageToClient(IPackage package, Client client, bool checkClientState)
    {
        if (package.To != null && package.To != client)
        {
            return false;
        }

        if (package.Except != null && package.Except == client)
        {
            return false;
        }

        return !checkClientState || client.State >= package.MinNeedState;
    }
}
