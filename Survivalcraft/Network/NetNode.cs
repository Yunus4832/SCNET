using System.Net;

using EntitySystem.Core;

using Game.Network.Enums;
using Game.Network.ModFileService;
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


    // 创建客户
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
                    packageItem.Handle(this, IsServer);
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
#if DEBUG
        Log.Information("[排队]添加到处理队列：" + package.ID);
#endif
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
#if DEBUG
                Log.Information("检测到有未处理的包:" + list.Count);
#endif
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
            rejectPackage.Handle(this, IsServer);
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
                requestPackage.Handle(this, true);
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
                if (SettingsManager.StartModServer && !string.IsNullOrEmpty(SettingsManager.ModServerAddress))
                {
                    ModFileServer.StartServer(SettingsManager.ModServerAddress);
                    Log.Information("模组服务器已开启");
                }

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
                new ConnectionRequestPackage(TokenId, VersionsManager.ServerVersion, SettingsManager.CommunityAccessUser,
                    SettingsManager.OnlineAccessToken, passwd, ModsManager.ModList), ep, false);
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
#if DEBUG
            Log.Information($"Client[{client.ID}]已收到Client[{PendingClient?.ID}]加入通知");
#endif
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
#if DEBUG
        Log.Information($"Client[{PendingClient.ID}]已完成加入过程");
#endif
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
            _pendingPackages.Clear();
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
                    c.Handle(node, IsServer);
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
        var writer = new PackageStreamWriter();
        writer.IsServer = IsServer;
        foreach (var package in packages)
        {
            writer.Write(_verifyByte);
            writer.Write(package.ID);
            package.WriteData(writer);
        }

        var w = CommonLib.GetWriter(writer, out _);
        if (netPeer != null)
        {
            if (!useDeliveryEvent)
            {
                netPeer.Send(w, DeliveryMethod.ReliableOrdered);
            }
            else
            {
                netPeer.SendWithDeliveryEvent(w, 0, DeliveryMethod.ReliableOrdered, netPeer);
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
        //先处理UI操作，Inventory序号增加后再处理客户端过来的包
        //发送物品同步数据
        SubsystemInventories.FlushSyncItems();

        foreach (var pair in _clientsToRemove)
        {
            RemoveClientImmediate(pair.Key, pair.Value);
        }

        _clientsToRemove.Clear();
        //批处理发送包队列，0.05s发送一次
        if (_pendingPackages.Count > 0 && Time.PeriodicEvent(0.05, 0.0))
        {
            lock (_pendingPackages)
            {
                if (CommonLib.WorkType == WorkType.Client)
                {
                    if (Clients.Count > 0)
                    {
                        var c = Clients[0];
                        var writer = new PackageStreamWriter();
                        writer.IsServer = IsServer;
                        var hasWrite = false;
                        foreach (var packet in _pendingPackages)
                        {
                            if (packet.To != null && packet.To != c)
                            {
                                continue;
                            }

                            if (packet.Except != null && packet.Except == c)
                            {
                                continue;
                            }

                            writer.Write(_verifyByte);
                            writer.Write(packet.ID);
                            packet.WriteData(writer);
                            hasWrite = true;
                        }

                        if (hasWrite && c.Peer != null)
                        {
                            c.Peer.Send(CommonLib.GetWriter(writer, out var size), DeliveryMethod.ReliableOrdered);
                        }
                    }
                }
                else
                {
                    foreach (var c in Clients.Values)
                    {
                        if (c.IsConnected)
                        {
                            var writer = new PackageStreamWriter();
                            writer.IsServer = IsServer;
                            var hasWrite = false;
                            foreach (var packet in _pendingPackages)
                            {
                                if (packet.To != null && packet.To != c)
                                {
                                    continue;
                                }

                                if (packet.Except != null && packet.Except == c)
                                {
                                    continue;
                                }

                                if (c.State < packet.MinNeedState && CommonLib.WorkType != WorkType.Client)
                                {
                                    continue;
                                }

                                writer.Write(_verifyByte);
                                writer.Write(packet.ID);
                                packet.WriteData(writer);
                                hasWrite = true;
                            }

                            if (!hasWrite)
                            {
                                continue;
                            }

                            if (c.Peer == null)
                            {
                                continue;
                            }

                            c.Peer.Send(CommonLib.GetWriter(writer, out var size),
                                DeliveryMethod.ReliableOrdered);
                        }
                    }
                }

                _pendingPackages.Clear();
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
}
