using System.Net;

using Engine.Network;
using Engine.Network.Security;

using EntitySystem.Core;

using Game.Admission;
using Game.Authentication;
using Game.Commands;
using Game.Network.Diagnostics;
using Game.Network.Handling;
using Game.Network.Transport;
using Game.Protocol.Identity;
using Game.Servers;

using LiteNetLib.Utils;

namespace Game.Network;

public class NetNode
{
    internal TemporaryJoinAuthorization? TemporaryAuthentication { get; set; }

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
        NotConnected = 0,

        Bootstrapping = 1,

        Connected = 2
    }


    private NetworkHost? _broadcastHost;

    private readonly Dictionary<Client, string> _clientsToRemove = new(ReferenceEqualityComparer.Instance);

    private P2PRoomController _p2p = new();

    public P2PRoomSettings P2PConfiguration => _p2p.Configuration;

    public P2PRoomStatus GetP2PStatus() => new(
        _p2p.State,
        _p2p.DesiredEnabled,
        IsServer && _host is not null,
        IsServer && GameManager.Project is not null,
        _p2p.Configuration.ServiceName,
        _p2p.Configuration.ControlUrl,
        _p2p.Configuration.Name,
        _p2p.Configuration.Description,
        _p2p.Configuration.IsPublic,
        _p2p.Capacity,
        _p2p.Registration?.SessionId,
        _p2p.Registration?.JoinCode,
        _p2p.LastError,
        _p2p.NextRetryAt
    );

    public void StartP2P(P2PRoomSettings configuration) => _p2p.Start(configuration);

    public void StopP2P() => _p2p.Stop();

    private NetworkHost? _host;

    private CancellationTokenSource? _connectingCancellation;

    private Task<NetworkConnection>? _connecting;

    private ConnectionPreparation? _preparedPath;

    private Task? _pathClosing;

    public NetworkTarget? RemoteTarget { get; private set; }

    private readonly Dictionary<Guid, Client> _transportClients = new();

    private readonly List<(Client Client, Task Completion)> _deliveryReceipts = [];

    public bool IsTransportRunning => _host is not null;

    public int? LocalPort => _host?.LocalPort;

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

    private bool _keepAdmissionScreenOnStop;

    internal event Func<bool>? AdmissionTransportInterrupted;

    public Action<Client>? OnClientStateChanged;

    public Action<Client>? OnClientTransportConnected;

    public Action<Client>? OnClientBootstrapApplied;

    public Action<Client>? OnClientBecameLive;

    public NetworkConnection? PendingPeer;

    public Guid TokenId;

    public Guid ConnectionEpoch { get; set; }

    public ConnectionPhase CurrentConnectionPhase { get; set; }

    public Client? Self { get; set; }

    public Client? Server { get; set; }

    public string Error { get; private set; } = string.Empty;

    public Stage CurrentStage { get; set; } = Stage.NotConnected;

    public WorldRole Role { get; internal set; } = WorldRole.None;

    public bool IsConnected => CurrentStage == Stage.Connected;

    public bool IsServer => Role == WorldRole.Server;

    private LocalPlayerAdmission? _localAdmission;

    private Guid _localAdmissionToken;


    internal void BeginLocalAdmission()
    {
        if (!IsServer ||
            _host is null ||
            GameManager.Project is not { } world ||
            AdmissionController is not { } controller ||
            !ReferenceEquals(controller.World, world) ||
            Self?.PlayerAddress is not null)
        {
            throw new InvalidOperationException("Start the server world before joining a local player.");
        }

        LocalAdmissionError = null;
        using var player = LocalIdentityManager.Current.OpenPlayerIdentity();
        _localAdmissionToken = Guid.NewGuid();
        if (PlayerAdmission.Rejection(
                world,
                this,
                new PlayerAddress(player.PublicIdentity.Address),
                _localAdmissionToken
            ) is { } error)
        {
            EndAdmission(error);
            return;
        }

        _isStopping = false;
        _localAdmission = new LocalPlayerAdmission(
            world,
            new AdmissionAttempt(
                controller.Policy,
                player.PublicIdentity.Address,
                _host.LocalIdentity.Address,
                System.Security.Cryptography.RandomNumberGenerator.GetBytes(32),
                DateTimeOffset.UtcNow
            )
        );
        AdmissionAccepted = false;
        PendingAdmissionChallenge = _localAdmission.Describe();
        AdmissionDeadline = DateTimeOffset.FromUnixTimeSeconds(_localAdmission.Challenge.ExpiresAt);
        if (controller.Policy.Mode == ServerAdmissionMode.Open)
        {
            TrySubmitAdmission(new AdmissionProofPackage(PendingAdmissionChallenge.Nonce));
        }
        else { AdmissionRequired?.Invoke(PendingAdmissionChallenge); }
    }

    internal string? LocalAdmissionError { get; private set; }

    internal bool HasPendingAdmission => _localAdmission is not null || CurrentStage == Stage.Bootstrapping;

    internal void EndAdmission(string error = "")
    {
        if (IsServer)
        {
            _localAdmission = null;
            PendingAdmissionChallenge = null;
            AdmissionDeadline = null;
            AdmissionAccepted = false;
            LocalAdmissionError = error;
            return;
        }

        Stop(error);
    }

    internal void PumpLocalAdmission()
    {
        if (_isStopping || _localAdmission is null) { return; }

        if (!ReferenceEquals(_localAdmission.World, GameManager.Project))
        {
            EndAdmission("世界已变化，请重新加入");
            return;
        }

        if (_localAdmission.Poll(DateTimeOffset.UtcNow) is not { } result) { return; }

        if (result.Code == AdmissionResultCode.Accepted)
        {
            var project = GameManager.Project!;
            var subject = new PlayerAddress(_localAdmission.Challenge.Subject);
            var token = _localAdmissionToken;
            try
            {
                if (!TryAdmitLocalPlayer(project, subject, token, out var error))
                {
                    EndAdmission(error!);
                    return;
                }

                QueuePackage(
                    new ClientAddedPackage { Client = ClientPackageBuilder.Capture(Self!) },
                    PackageAudience.Global
                );
            }
            catch (Exception exception)
            {
                Log.Error(exception);
                LeaveLocalPlayer();
                EndAdmission("玩家数据加载失败，请稍后重试");
                return;
            }
        }

        ApplyAdmissionResult(result);
        if (result.Code == AdmissionResultCode.Accepted) { _localAdmission = null; }
    }

    public void LeaveLocalPlayer(string reason = "")
    {
        if (!IsServer || Self is not { } client) { return; }

        Self = null;
        RemoveClientImmediate(client, reason);
        AdmissionAccepted = false;
        TemporaryAuthentication = null;
    }

    internal bool IsClosing => _isStopping;

    internal ServerAdmissionController? AdmissionController { get; private set; }

    public AdmissionChallengePackage? PendingAdmissionChallenge { get; private set; }

    public DateTimeOffset? AdmissionDeadline { get; private set; }

    public bool AdmissionAccepted { get; private set; }

    private ReceivedPackage? _admissionBootstrap;

    public event Action<AdmissionChallengePackage>? AdmissionRequired;

    public event Action<AdmissionResultPackage>? AdmissionResultReceived;

    private NetworkConnection? _admissionServerPeer;

    public int ClientCount => Clients.Values.Count(client => client.PlayerAddress is not null);

    public bool IsPlayerOnline(Client client)
    {
        return client.PlayerAddress is not null &&
               client.State != ClientState.NotConnected &&
               Clients.TryGetValue(client.ID, out var registered) &&
               ReferenceEquals(client, registered) &&
               (client.Peer is null || client.Peer.IsConnected);
    }


    internal Client? ResolveSender(
        NetworkConnection? peer = null,
        NetworkConnectionRequest? request = null,
        IPEndPoint? endpoint = null
    )
    {
        if (peer is not null)
        {
            if (!_transportClients.TryGetValue(peer.Id, out var client))
            {
                client = new Client(peer);
                _transportClients.Add(peer.Id, client);
            }

            return client;
        }

        return request is not null ? new Client(request) : endpoint is not null ? new Client(endpoint) : null;
    }

    internal Client? PendingPlayer => PendingClient;

    private Client? PendingClient => PendingPeer is null ? null : ResolveSender(peer: PendingPeer);

    public IEnumerable<Client> Peers => Clients.Values.Where(client => client.Peer is not null);

    // 当一个客户端收到一个消息
    public event Action<NetNode, IEnumerable<ReceivedPackage>>? OnReceive;

    //添加到队列
    public void QueuePackage(IProtocolMessage package, PackageAudience audience)
    {
        QueueOutboundPackage(new OutboundPackage(package, audience));
    }

    private void QueueOutboundPackage(OutboundPackage outboundPackage)
    {
        lock (_pendingPackages)
        {
            if (_isStopping)
            {
                outboundPackage.Fanout?.Advance(1, 0);
                return;
            }

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
    public Client CreateClient(NetworkConnection peer, Guid tokenId, Guid guid)
    {
        if (!IsServer)
        {
            throw new InvalidOperationException("creating client as non-server");
        }

        var client = new Client(peer, FindUnusedIndex(), tokenId, guid, GameManager.Project);
        _transportClients[peer.Id] = client;
        return client;
    }

    internal bool CanReceiveFromSession(Client? sender, IProtocolMessage package)
    {
        return sender is not null &&
               (Clients.TryGetValue(sender.ID, out var registered) && ReferenceEquals(registered, sender) ||
                package is AdmissionProofPackage);
    }

    internal void ReceiveAdmissionChallenge(Client sender, AdmissionChallengePackage challenge)
    {
        if (IsServer || CurrentStage != Stage.Bootstrapping || sender.Peer is not { } peer) { return; }

        if (AdmissionAccepted)
        {
            Stop("服务器在认证通过后更换了挑战");
            return;
        }

        SCNET.Authentication.Protocol.Security.AuthenticationNonce.Validate(challenge.Nonce);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (challenge.ExpiresAt <= now ||
            challenge.ExpiresAt >
            now +
            SCNET.Authentication.Protocol.AuthenticationProtocol.MaximumChallengeLifetimeSeconds +
            SCNET.Authentication.Protocol.AuthenticationProtocol.ClockSkewSeconds)
        {
            Stop("服务器认证挑战已过期或无效");
            return;
        }

        if (PendingAdmissionChallenge is { } pending)
        {
            if (ReferenceEquals(_admissionServerPeer, peer) && pending == challenge) { return; }

            Stop("服务器在认证期间更换了挑战");
            return;
        }

        _admissionServerPeer = peer;
        PendingAdmissionChallenge = challenge;
        AdmissionDeadline = DateTimeOffset.FromUnixTimeSeconds(challenge.ExpiresAt);
        if (challenge.Mode == ServerAdmissionMode.Open)
        {
            TrySubmitAdmission(new AdmissionProofPackage(challenge.Nonce));
        }
        else { AdmissionRequired?.Invoke(challenge); }
    }

    internal string? AdmissionServerAddress =>
        _localAdmission?.Challenge.Server ?? _admissionServerPeer?.PeerIdentity.Address;

    internal SCNET.Authentication.Protocol.Contracts.AdmissionChallenge CaptureAdmissionChallenge()
    {
        if (_localAdmission is { } local && PendingAdmissionChallenge?.Nonce == local.Challenge.Nonce)
        {
            return local.Challenge;
        }

        if (PendingAdmissionChallenge is not { } challenge || _admissionServerPeer is not { } peer || _host is null)
        {
            throw new InvalidOperationException("No active external admission challenge.");
        }

        return new SCNET.Authentication.Protocol.Contracts.AdmissionChallenge(
            _host.LocalIdentity.Address,
            peer.PeerIdentity.Address,
            challenge.Nonce,
            SCNET.Authentication.Protocol.Security.AuthenticationNonce.Encode(peer.ChannelBinding.Span),
            challenge.ExpiresAt
        );
    }

    internal bool CanSubmitAdmission(string nonce)
    {
        return !_isStopping &&
               HasPendingAdmission &&
               PendingAdmissionChallenge?.Nonce == nonce &&
               (_localAdmission is not null || _admissionServerPeer is { IsConnected: true });
    }

    public bool TrySubmitAdmission(AdmissionProofPackage proof)
    {
        if (!CanSubmitAdmission(proof.Nonce)) { return false; }

        if (_localAdmission is { } local) { return local.Submit(proof); }

        SendWriterFromPackage(proof, _admissionServerPeer!);
        return true;
    }

    internal void ReceiveAdmissionResult(Client sender, AdmissionResultPackage result)
    {
        if (_isStopping ||
            !ReferenceEquals(sender.Peer, _admissionServerPeer) ||
            PendingAdmissionChallenge?.Nonce != result.Nonce)
        {
            return;
        }

        ApplyAdmissionResult(result);
    }

    private void ApplyAdmissionResult(AdmissionResultPackage result)
    {
        if (result.Code == AdmissionResultCode.Accepted)
        {
            AdmissionAccepted = true;
            PendingAdmissionChallenge = null;
            AdmissionDeadline = DateTimeOffset.UtcNow.AddMilliseconds(CommonLib.DisconnectTimeout);
        }

        AdmissionResultReceived?.Invoke(result);
        if (result.Code == AdmissionResultCode.Accepted && !_isStopping && _admissionBootstrap is { } bootstrap)
        {
            _admissionBootstrap = null;
            if (OnReceive is { } receive) { receive(this, [bootstrap]); }
            else { AddPendingHandlePackage(bootstrap); }
        }

        if (!_isStopping &&
            result.Code is AdmissionResultCode.Cancelled or AdmissionResultCode.Expired or AdmissionResultCode.Denied)
        {
            EndAdmission("认证已取消、过期或被拒绝");
        }
    }

    public void AddClient(Client client)
    {
        Clients[client.ID] = client;
        if (client.Peer is not null && (IsServer || client.ID == 0))
        {
            _transportClients[client.Peer.Id] = client;
        }
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
    public event Action<string>? LocalPlayerRemoved;

    public void RemoveClientImmediate(Client client, string reason = "")
    {
        var localPlayer = ReferenceEquals(client, Self);
        if (localPlayer && !IsServer)
        {
            Stop(reason);
            return;
        }

        if (localPlayer) { Self = null; }

        if (client.Peer != null && client.Peer.IsConnected)
        {
            if (string.IsNullOrEmpty(reason))
            {
                client.Peer.Disconnect();
            }
            else
            {
                client.Peer.Disconnect(EncodeDisconnectReason(reason));
            }
        }

        if (client == Server)
        {
            Stop();
            CurrentStage = Stage.NotConnected;
            return;
        }

        if (IsServer && client.PlayerAddress is { } playerAddress)
        {
            GameManager.Project?.FindSubsystem<SubsystemTeams>()?.CancelPendingOperations(playerAddress);
        }

        if (Clients.TryGetValue(client.ID, out var registered) && ReferenceEquals(registered, client))
        {
            if (IsServer)
            {
                QueuePackage(new ClientRemovedPackage { ClientId = client.ID }, PackageAudience.Global);
            }

            client.State = ClientState.NotConnected;
            OnClientStateChanged?.Invoke(client);
            Clients.Remove(client.ID);
        }

        if (localPlayer && !_isStopping) { LocalPlayerRemoved?.Invoke(reason); }
    }

    /// <summary>
    ///     服务器关闭，不执行OnClientStateChanged，防止多次Dispose
    /// </summary>
    public void RemoveAllClients(string reason = "")
    {
        var clients = new List<Client>(Clients.Values);
        foreach (var client in clients)
        {
            if (IsServer && client.PlayerAddress is { } playerAddress)
            {
                GameManager.Project?.FindSubsystem<SubsystemTeams>()?.CancelPendingOperations(playerAddress);
            }

            if (client == Self)
            {
                continue;
            }

            if (client.Peer != null && client.Peer.IsConnected)
            {
                if (reason != null)
                {
                    client.Peer.Disconnect(EncodeDisconnectReason(reason));
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

    public Client? GetClientByCharacterId(Guid? guid, bool throwIfNull = false)
    {
        var client = Clients.Values.FirstOrDefault(c => c.CharacterId == guid);
        if (client is null && throwIfNull)
        {
            throw new InvalidOperationException("Client not found");
        }

        return client;
    }

    private void HandleDatagram(NetworkDatagramEndpoint source, byte[] payload)
    {
        UnconnectedReceiveEvent(source.TransportEndpoint, payload, false);
    }

    private void HandleBroadcastDatagram(NetworkDatagramEndpoint source, byte[] payload)
    {
        UnconnectedReceiveEvent(source.TransportEndpoint, payload, true);
    }

    /// <summary>
    ///     连接断开事件 服务端
    /// </summary>
    private void PeerDisconnectedEvent(NetworkConnection peer, NetworkDisconnectInfo disconnectInfo)
    {
        AdmissionController?.Forget(peer);
        Log.Information($"{peer.TransportEndpoint}连接关闭: " + disconnectInfo.Reason);
        var str = DecodeDisconnectReason(disconnectInfo);

        if (Role == WorldRole.RemoteClient)
        {
            Log.Debug(
                $"Client transport disconnected: stage={CurrentStage}, admitted={AdmissionAccepted}, admissionOwner={AdmissionTransportInterrupted is not null}."
            );
            if (CurrentStage == Stage.Bootstrapping &&
                !AdmissionAccepted &&
                AdmissionTransportInterrupted?.Invoke() == true)
            {
                _keepAdmissionScreenOnStop = true;
                Stop();
            }
            else { Stop(str); }
        }
        else
        {
            if (!_transportClients.Remove(peer.Id, out var client))
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
                DeliveryEvent(null);
            }
        }
    }

    /// <summary>
    ///     连接断开事件 客户端
    /// </summary>
    /// <param name="remoteEndPoint"></param>
    /// <param name="payload"></param>
    /// <param name="fromBroadcast"></param>
    private void UnconnectedReceiveEvent(IPEndPoint remoteEndPoint, ReadOnlyMemory<byte> payload, bool fromBroadcast)
    {
        if (CurrentStage == Stage.NotConnected)
        {
            return;
        }

        var list = PackageManager.DecodePackages(this, payload, ResolveSender(endpoint: remoteEndPoint));
        foreach (var packageItem in list)
        {
            if (packageItem.Package is not ServerInfoPackage) { continue; }

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

    public void AddPendingHandlePackage(ReceivedPackage package)
    {
        Log.Debug("[排队]添加到处理队列：" + package.Package.GetType().Name);
        _pendingHandlePackages.Add(package);
    }

    /// <summary>
    ///     数据接收事件
    /// </summary>
    /// <param name="peer"></param>
    /// <param name="message"></param>
    private void NetworkReceiveEvent(NetworkConnection peer, NetworkMessage message)
    {
        if (_isStopping || CurrentStage == Stage.NotConnected)
        {
            return;
        }

        var fromClient = ResolveSender(peer: peer);
        var list = PackageManager.DecodePackages(this, message.Payload, fromClient);
        if (IsServer)
        {
            list.RemoveAll(received => !CanReceiveFromSession(fromClient, received.Package));
        }

        else if (!AdmissionAccepted && CurrentStage == Stage.Bootstrapping)
        {
            foreach (var received in list)
            {
                if (received.Package is BootstrapPackage) { _admissionBootstrap ??= received; }
            }

            // Bulk Bootstrap can overtake the control-channel acceptance. Keep one bounded copy until accepted.
            list.RemoveAll(received => received.Package is not AdmissionChallengePackage and not AdmissionResultPackage
                and not ConnectionRejectPackage
            );
        }

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
    }

    /// <summary>
    ///     新客户端连接事件
    /// </summary>
    /// <param name="request"></param>
    private void ConnectionRequestEvent(NetworkConnectionRequest request)
    {
        Log.Information("接收到连接请求 " + request.TransportEndpoint);

        if (GameManager.Project == null)
        {
            SendWriterFromPackage(new ConnectionRejectPackage("请等待服务器启动完成后加入"), request, true);
        }
        else if (PendingClient != null)
        {
            Log.Information($"Too Many Request And Reject Current Request:{request.TransportEndpoint}");
            Log.Information($"Current Pending Client:{PendingClient}");
            SendWriterFromPackage(
                new ConnectionRejectPackage($"当前服务器连接请求太多，请稍候再试，等待客户端回应数{AgreeOnPendingPeer.Count}"),
                request,
                true
            );
        }
        else
        {
            try
            {
                var requestPackage =
                    PackageManager.DecodePackage<ConnectionRequestPackage>(
                        this,
                        request.Handshake,
                        ResolveSender(request: request)
                    );
                PackageDispatcher.Handle(requestPackage.Package, requestPackage.Context);
            }
            catch (Exception e)
            {
                SendWriterFromPackage(new ConnectionRejectPackage($"错误的连接请求数据包:{e.Message}"), request, true);
                Log.Error($"ConnectionRequestEvent Error: {e}");
            }
        }
    }

    public void PrepareSinglePlayerWorld()
    {
        Role = WorldRole.SinglePlayer;
        TemporaryAuthentication = null;
        _isStopping = false;
        Clients.Clear();
        Self = null;
        Server = null;
        CurrentStage = Stage.NotConnected;
    }

    internal void JoinSinglePlayerWorld()
    {
        if (Role != WorldRole.SinglePlayer || GameManager.Project is not { } world || Self is not null)
        {
            throw new InvalidOperationException("Load the single-player world before joining its local player.");
        }

        using var identity = LocalIdentityManager.Current.OpenPlayerIdentity();
        if (!TryAdmitLocalPlayer(
                world,
                new PlayerAddress(identity.PublicIdentity.Address),
                Guid.NewGuid(),
                out var error
            ))
        {
            throw new InvalidOperationException(error);
        }
    }

    private bool TryAdmitLocalPlayer(Project world, PlayerAddress subject, Guid token, out string? error)
    {
        if (!PlayerAdmission.TryCreateSession(
                world,
                this,
                subject,
                token,
                character => new Client(null, FindUnusedIndex(), token, character, world) { PlayerAddress = subject },
                out var client,
                out error
            ))
        {
            return false;
        }

        Self = client;
        AddClient(client!);
        GameManager.ActivateLocalPlayer(world, client!);
        return true;
    }

    public void PrepareServerWorld()
    {
        TemporaryAuthentication = null;
        _isStopping = false;
        Clients.Clear();
        Self = null;
        Server = new Client(Guid.Empty, GameManager.Project!);
        AddClient(Server);
        Role = WorldRole.Server;
        CurrentStage = Stage.NotConnected;
    }

    private NetworkHost CreateHost(int port, NetworkIdentity identity)
    {
        var host = new NetworkHost(
            port,
            new NetworkHostOptions
            {
                Identity = identity,
                ReuseAddress = true,
                DisconnectTimeout = TimeSpan.FromMilliseconds(CommonLib.DisconnectTimeout)
            }
        );
        AttachHostEvents(host);
        return host;
    }

    private void AttachHostEvents(NetworkHost host)
    {
        host.ConnectionRequested += ConnectionRequestEvent;
        host.DatagramReceived += HandleDatagram;
        host.Received += NetworkReceiveEvent;
        host.Disconnected += PeerDisconnectedEvent;
    }

    /// <summary>
    ///     开启服务器
    /// </summary>
    /// <param name="port"></param>
    /// <param name="broadcastPort"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public bool StartServer(
        int port,
        int? broadcastPort,
        int maxParticipants,
        P2PRoomSettings room,
        ServerAdmissionPolicy? policyOverride = null,
        string? serverAddress = null
    )
    {
        if (_host is not null)
        {
            throw new InvalidOperationException("已经开启服务器");
        }

        try
        {
            _isStopping = false;
            var project = GameManager.Project ??
                          throw new InvalidOperationException("Load the admitted world before starting its listener.");
            var policy = policyOverride ??
                         ServerAdmissionPolicy.Load(project.ExtensionData.Get("game", "admission"));
            using (var identity = LocalIdentityManager.Current.OpenServerIdentity(serverAddress))
            {
                if (Server is null || Role != WorldRole.Server)
                {
                    throw new InvalidOperationException(
                        "Prepare the authoritative server world before opening its listener."
                    );
                }

                Server.SetProject(project);
                AdmissionController = new ServerAdmissionController(
                    this,
                    project,
                    policy,
                    identity.PublicIdentity.Address
                );
                _host = CreateHost(port, identity);
            }

            _localAdmission = null;
            if (broadcastPort.HasValue)
            {
                _broadcastHost = new NetworkHost(
                    broadcastPort.Value,
                    new NetworkHostOptions
                    {
                        ReceiveBroadcasts = true
                    }
                );
                _broadcastHost.DatagramReceived += HandleBroadcastDatagram;
            }

            Log.Information($"开启服务器成功，端口 {_host.LocalPort}");
            Log.Information($"Server identity: {_host.LocalIdentity.Address}");
            Window.Frame += Update;
            Window.Closed += StopImmediate;
            CurrentStage = Stage.Connected;
            Role = WorldRole.Server;
            _p2p.Bind(_host, room, maxParticipants);
            return true;
        }
        catch (Exception e)
        {
            StopImmediate();
            Log.Error($"Failed to start server: {e}");
            throw;
        }
    }

    public void ConnectServer(ServerConnectionPreparation prepared)
    {
        if (_host is not null)
        {
            throw new InvalidOperationException("连接已启动");
        }

        var transport = prepared.Consume();
        TemporaryAuthentication = null;
        try
        {
            _isStopping = false;
            Log.Information("Connecting to prepared server");
            Clients.Clear();
            _host = transport.Host;
            _preparedPath = transport.Path;
            RemoteTarget = prepared.Target;
            AttachHostEvents(_host);
            _connectingCancellation = new CancellationTokenSource();
            TokenId = Guid.NewGuid();
            var request = new ConnectionRequestPackage(
                TokenId,
                VersionsManager.ProtocolVersion,
                CurrentModRuntime.Value?.ModDataHash ?? ModProfileManager.EmptyDataHash
            );
            CurrentStage = Stage.Bootstrapping;
            Role = WorldRole.RemoteClient;
            _connecting = _preparedPath.ConnectAsync(CreateUnconnectedPayload(request), _connectingCancellation.Token);
            Window.Frame += Update;
            Window.Closed += StopImmediate;
        }
        catch (Exception e)
        {
            Error = e.Message;
            StopImmediate();
        }
    }

    /// <summary>
    ///     发送回调事件
    /// </summary>
    /// <param name="client"></param>
    public void DeliveryEvent(Client? client)
    {
        if (client is not null)
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
        TemporaryAuthentication = null;
        _isStopping = true;
        Error = error;
    }

    private static void StopHost(ref NetworkHost? host)
    {
        var closing = host;
        host = null;
        try
        {
            closing?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        catch (Exception e)
        {
            Log.Warning($"Transport stopped with an error: {e.Message}");
        }
    }

    public void StopImmediate() => StopImmediate(false);

    internal void StopForAdmissionRecovery()
    {
        if (AdmissionAccepted || CurrentStage == Stage.Connected)
        {
            throw new InvalidOperationException("An admitted world session cannot resume a pending authentication.");
        }

        StopImmediate(true);
    }

    private void StopImmediate(bool keepAdmissionScreen)
    {
        TemporaryAuthentication = null;
        var wasClient = Role == WorldRole.RemoteClient;
        _isStopping = true;
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
            StopHost(ref _broadcastHost);

            // 先发送提示，然后再关闭服务器
            if (Role == WorldRole.Server)
            {
                RemoveAllClients("服务器主动关闭");
            }

            _ = _p2p.ShutdownAsync();
            _p2p = new P2PRoomController();
            _connectingCancellation?.Cancel();
            StopHost(ref _host);
            if (_connecting is not null)
            {
                try
                {
                    _connecting.GetAwaiter().GetResult();
                }
                catch (Exception)
                {
                    // Closing cancels any unfinished attempt; the normal update path reports failures.
                }

                _connecting = null;
            }

            if (_preparedPath is not null)
            {
                _pathClosing = _preparedPath.DisposeAsync().AsTask();
                _preparedPath = null;
            }

            if (_pathClosing is not null)
            {
                try
                {
                    _pathClosing.GetAwaiter().GetResult();
                }
                catch (Exception e)
                {
                    Log.Warning($"Could not release connection preparation: {e.Message}");
                }

                _pathClosing = null;
            }

            RemoteTarget = null;
            _connectingCancellation?.Dispose();
            _connectingCancellation = null;
            foreach (var receipt in _deliveryReceipts)
            {
                _ = receipt.Completion.Exception;
            }

            _deliveryReceipts.Clear();
            _transportClients.Clear();
            AdmissionController = null;
            _localAdmission = null;
            LocalAdmissionError = null;
            PendingAdmissionChallenge = null;
            AdmissionDeadline = null;
            _admissionServerPeer = null;
            AdmissionAccepted = false;
            _admissionBootstrap = null;
        }
        catch (Exception e)
        {
            Log.Error(e);
        }
        finally
        {
            Window.Closed -= StopImmediate;
            Window.Frame -= Update;
            OnReceive = null;
            OnClientStateChanged = null;
            OnClientTransportConnected = null;
            OnClientBootstrapApplied = null;
            OnClientBecameLive = null;
            ConnectionEpoch = Guid.Empty;
            CurrentConnectionPhase = ConnectionPhase.TransportConnected;
            CurrentStage = Stage.NotConnected;
            Role = WorldRole.None;
            _keepAdmissionScreenOnStop = false;
            if (!keepAdmissionScreen && wasClient)
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

            Clients.Clear();
            Self = null;
            Server = null;
            _pendingHandlePackages.Clear();

            if (!keepAdmissionScreen && !string.IsNullOrEmpty(Error))
            {
                DialogsManager.Alert(Error);
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

    public void SendWriterFromPackage(IProtocolMessage package, IPEndPoint endPoint, bool isSend = true)
    {
        SendWriterFromPackages([package], null, null, endPoint, isSend);
    }

    public void SendWriterFromPackage(
        IProtocolMessage package,
        NetworkConnection? netPeer,
        bool useDeliveryEvent = false
    )
    {
        SendWriterFromPackages([package], netPeer, null, null, useDeliveryEvent);
    }

    /// <summary>
    ///     发送一个数据包到还未连接的远程
    /// </summary>
    /// <param name="package">数据包</param>
    /// <param name="request">远程连接请求</param>
    /// <param name="reject">发送后是否拒绝连接 true拒绝 false不拒绝</param>
    public void SendWriterFromPackage(IProtocolMessage package, NetworkConnectionRequest request, bool reject)
    {
        if (reject)
        {
            request.Reject(CreateUnconnectedPayload(package));
        }
        else
        {
            SendWriterFromPackages([package], null, request);
        }
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
        IEnumerable<IProtocolMessage> packages,
        NetworkConnection? netPeer = null,
        NetworkConnectionRequest? request = null,
        IPEndPoint? iPEndPoint = null,
        bool useDeliveryEvent = false
    )
    {
        var packageList = packages as IList<IProtocolMessage> ?? packages.ToList();
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
                netPeer.Send(w.Data.AsSpan(0, w.Length), transport.ChannelNumber, transport.Delivery);
            }
            else
            {
                var receipt = netPeer.SendWithReceipt(
                    w.Data.AsSpan(0, w.Length),
                    transport.ChannelNumber,
                    transport.Delivery
                );
                _deliveryReceipts.Add((ResolveSender(peer: netPeer)!, receipt.Completion));
            }

            SendStatistics.Record(transport.Channel, w.Length, measurements);
            foreach (var package in packageList)
            {
                SendStatistics.RecordFanout(package.GetType(), transport.Channel, 1);
            }
        }
        else if (request != null)
        {
            // Transport rejection data is bounded; keep malformed-request diagnostics in logs.
            request.Reject(
                w.Length <= 256
                    ? w.Data.AsSpan(0, w.Length)
                    : CreateUnconnectedPayload(new ConnectionRejectPackage("连接请求被拒绝"))
            );
        }
        else if (iPEndPoint != null)
        {
            if (!useDeliveryEvent)
            {
                throw new InvalidOperationException("Use ConnectServer to start a connection.");
            }

            _host!.SendDatagram(iPEndPoint, w.Data.AsSpan(0, w.Length));
        }
    }

    internal static byte[] CreateUnconnectedPayload(IProtocolMessage package)
    {
        return ProtocolPacketCodec.Encode([package]);
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
            StopImmediate(_keepAdmissionScreenOnStop);
        }
        else
        {
            UpdateTransport();
        }
    }

    private void UpdateTransport()
    {
        _p2p.Update();
        if (_p2p.AutomaticPending &&
            _p2p.State is P2PRoomState.Disabled &&
            IsServer &&
            GameManager.Project is { } project &&
            CurrentModRuntime.Value is not null)
        {
            _p2p.ConsumeAutomaticRequest();
            var result = CommandExecutor.ExecuteSystem(new StartP2PCommand(), project);
            if (!result.Success)
            {
                Log.Warning($"Could not automatically start P2P room: {CommandText.Resolve(result)}");
            }
        }

        _host?.DispatchEvents();
        AdmissionController?.Update();
        _broadcastHost?.DispatchEvents();
        if (_host?.Failure is { } failure)
        {
            Stop(failure.Message);
        }

        if (_connecting is { IsCompleted: true } connecting)
        {
            _connecting = null;
            try
            {
                ResolveSender(peer: connecting.GetAwaiter().GetResult());
            }
            catch (NetworkConnectionException e)
            {
                Stop(DecodeDisconnectReason(e.Information));
            }
            catch (Exception e)
            {
                Stop(e.Message);
            }
        }

        if (_connecting is null && _preparedPath is not null)
        {
            _pathClosing = _preparedPath.DisposeAsync().AsTask();
            _preparedPath = null;
        }

        if (_pathClosing is { IsCompleted: true } closed)
        {
            _pathClosing = null;
            if (closed.IsFaulted)
            {
                Log.Warning(
                    $"Could not release connection preparation: {closed.Exception!.GetBaseException().Message}"
                );
            }
        }

        for (var index = _deliveryReceipts.Count - 1; index >= 0; index--)
        {
            var receipt = _deliveryReceipts[index];
            if (!receipt.Completion.IsCompleted)
            {
                continue;
            }

            _deliveryReceipts.RemoveAt(index);
            if (receipt.Completion.IsCompletedSuccessfully)
            {
                DeliveryEvent(receipt.Client);
            }
            else
            {
                _ = receipt.Completion.Exception;
                RemoveClient(receipt.Client, "传输确认失败");
            }
        }
    }

    private static byte[] EncodeDisconnectReason(string reason)
    {
        var writer = NetDataWriter.FromString(reason);
        if (writer.Length <= 256)
        {
            return writer.CopyData();
        }

        return NetDataWriter.FromString("连接已关闭").CopyData();
    }

    private string DecodeDisconnectReason(NetworkDisconnectInfo information)
    {
        if (!information.Data.IsEmpty)
        {
            try
            {
                var reject = PackageManager.DecodePackage<ConnectionRejectPackage>(this, information.Data);
                return reject.Package.Reason;
            }
            catch (Exception)
            {
                var reader = new NetDataReader(information.Data.ToArray());
                if (reader.TryGetString(out var reason))
                {
                    return reason;
                }
            }
        }

        return LanguageManager.GetContentWidgets("NetPlayScreen", $"Disconnect_{information.Reason}");
    }

    private void FlushPendingPackages()
    {
        List<OutboundPackage> packages;
        var deferredSnapshots = new HashSet<OutboundPackage>(OutboundPackageIdentityComparer.Instance);
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
                Fanout = outboundPackage.Fanout ??
                         new NetworkMessageFanout(
                             SendStatistics,
                             outboundPackage.Package.GetType(),
                             PackageTransportPolicy.Get(outboundPackage.Package).Channel
                         )
            };
        }

        if (Role == WorldRole.RemoteClient)
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

    private List<OutboundPackage> DequeueFlushablePackages(
        double now,
        HashSet<NetworkChannel> flushedChannels
    )
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
        HashSet<OutboundPackage> deferredSnapshots
    )
    {
        if (client.Peer is not { IsConnected: true })
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

            SendStatistics.RecordRouting(
                packages.Count,
                channelPackages.Count,
                Stopwatch.GetTimestamp() - routingStarted
            );

            if (channelPackages.Count == 0 || transport == null)
            {
                continue;
            }

            List<OutboundPackage> deferredPackages;
            try
            {
                deferredPackages = SendPackageBatches(client, channelPackages, transport.Value);
            }
            catch (NetworkSendException exception) when (exception.Result == NetworkSendResult.Closed)
            {
                // The transport pump can close a connection before the game dispatches its disconnect event.
                return;
            }

            foreach (var outboundPackage in deferredPackages)
            {
                deferredSnapshots.Add(outboundPackage with { Audience = PackageAudience.To(client) });
            }
        }
    }

    private List<OutboundPackage> SendPackageBatches(
        Client client,
        List<OutboundPackage> packages,
        PackageTransport transport
    )
    {
        if (packages.Count > ProtocolPacketCodec.MaximumMessagesPerPacket)
        {
            var deferred = new List<OutboundPackage>();
            var considered = transport.Coalesce && !CanFragment(transport.Delivery)
                ? ProtocolPacketCodec.MaximumMessagesPerPacket
                : packages.Count;
            for (var offset = 0; offset < considered; offset += ProtocolPacketCodec.MaximumMessagesPerPacket)
            {
                var count = Math.Min(ProtocolPacketCodec.MaximumMessagesPerPacket, considered - offset);
                deferred.AddRange(SendPackageBatches(client, packages.GetRange(offset, count), transport));
            }

            if (considered < packages.Count)
            {
                deferred.AddRange(packages.GetRange(considered, packages.Count - considered));
            }

            return deferred;
        }

        var peer = client.Peer!;
        if (packages.Any(outboundPackage => IsTerrainChunkPackage(outboundPackage.Package)))
        {
            foreach (var outboundPackage in packages)
            {
                var measurements = new List<(Type PackageType, int PayloadBytes)>();
                var chunkWriter = CreatePackageWriter([outboundPackage.Package], transport, out _, measurements);
                peer.Send(chunkWriter.Data.AsSpan(0, chunkWriter.Length), transport.ChannelNumber, transport.Delivery);
                SendStatistics.Record(transport.Channel, chunkWriter.Length, measurements);
                outboundPackage.Fanout!.DeliveredTo(client);
            }

            return [];
        }

        var maxPacketSize = peer.GetMaxPayloadSize(transport.Delivery);
        var remainingPackages = new List<OutboundPackage>(packages.Count);
        foreach (var outboundPackage in packages)
        {
            if (outboundPackage.Package is BodyStatesPackage
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
                        peer.Send(
                            chunkWriter.Data.AsSpan(0, chunkWriter.Length),
                            transport.ChannelNumber,
                            transport.Delivery
                        );
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
        var writer = CreatePackageWriter(
            remainingPackages.Select(item => item.Package),
            transport,
            out var packetSize,
            remainingMeasurements
        );
        if (packetSize <= maxPacketSize || CanFragment(transport.Delivery))
        {
            peer.Send(writer.Data.AsSpan(0, writer.Length), transport.ChannelNumber, transport.Delivery);
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
        int maxPacketSize
    )
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
        peer.Send(writer.Data.AsSpan(0, writer.Length), transport.ChannelNumber, transport.Delivery);
        SendStatistics.Record(transport.Channel, writer.Length, measurements);
        foreach (var outboundPackage in selectedPackages)
        {
            outboundPackage.Fanout!.DeliveredTo(client);
        }

        return deferredPackages;
    }

    internal List<BodyStatesPackage> SplitOversizedBodyPackage(
        BodyStatesPackage bodyPackage,
        PackageTransport transport,
        int maxPacketSize
    )
    {
        var chunks = new List<BodyStatesPackage>();
        var items = new List<SubsystemBodyPackage.BodyItem>();
        var candidate = bodyPackage with { BodyList = items };
        foreach (var item in bodyPackage.BodyList)
        {
            items.Add(item);
            CreatePackageWriter([candidate], transport, out var chunkSize);
            if (chunkSize <= maxPacketSize) { continue; }

            items.RemoveAt(items.Count - 1);
            if (items.Count > 0) { chunks.Add(bodyPackage with { BodyList = items.ToArray() }); }

            items.Clear();
            items.Add(item);
        }

        if (items.Count > 0) { chunks.Add(bodyPackage with { BodyList = items.ToArray() }); }

        return chunks;
    }

    private void SendPackageBatch(
        Client client,
        List<OutboundPackage> packages,
        PackageTransport transport,
        int maxPacketSize
    )
    {
        if (packages.Count == 0)
        {
            return;
        }

        var measurements = new List<(Type PackageType, int PayloadBytes)>();
        var peer = client.Peer!;
        var writer = CreatePackageWriter(
            packages.Select(item => item.Package),
            transport,
            out var packetSize,
            measurements
        );
        if (packetSize > maxPacketSize)
        {
            Log.Error(
                $"Dropping oversized {transport.Delivery} package batch " +
                $"({packetSize}/{maxPacketSize} bytes): {string.Join(", ", packages.Select(p => p.Package.GetType().Name))}"
            );
            return;
        }

        peer.Send(writer.Data.AsSpan(0, writer.Length), transport.ChannelNumber, transport.Delivery);
        SendStatistics.Record(transport.Channel, writer.Length, measurements);
        foreach (var outboundPackage in packages)
        {
            outboundPackage.Fanout!.DeliveredTo(client);
        }
    }

    private NetDataWriter CreatePackageWriter(
        IEnumerable<IProtocolMessage> packages,
        PackageTransport transport,
        out int packetSize,
        List<(Type PackageType, int PayloadBytes)>? measurements = null
    )
    {
        var packageList = packages as IReadOnlyCollection<IProtocolMessage> ?? packages.ToArray();
        var encoding = new ProtocolPacketMeasurements { Messages = measurements };
        var data = ProtocolPacketCodec.Encode(packageList, GetCompressionPolicy(packageList, transport), encoding);
        packetSize = data.Length;
        SendStatistics.RecordEncoding(
            packageList.Count,
            encoding.PayloadBytes,
            data.Length,
            encoding.SerializationTicks,
            encoding.FramingTicks
        );
        return CommonLib.GetWriter(data);
    }

    private static CompressionPolicy GetCompressionPolicy(
        IEnumerable<IProtocolMessage> packages,
        PackageTransport transport
    )
    {
        if (transport.Mode != TransportMode.Reliable)
        {
            return CompressionPolicy.None;
        }

        // Chunk payloads are independently compressed and cached by the terrain codec.
        return packages.Any(IsTerrainChunkPackage)
            ? CompressionPolicy.None
            : CompressionPolicy.Adaptive;
    }

    private static bool IsTerrainChunkPackage(IProtocolMessage package) =>
        package is SubsystemTerrainPackage
        {
            Type: SubsystemTerrainPackage.DataType.SyncTerrainChunkFragment
        };

    private static bool CanFragment(DeliveryMode deliveryMethod)
    {
        return deliveryMethod is DeliveryMode.ReliableOrdered or DeliveryMode.ReliableUnordered;
    }

    private void RecordDeferredSnapshot(IProtocolMessage package, int packetSize, int maxPacketSize)
    {
        _deferredSnapshotPackages++;
        var now = Time.RealTime;
        if (_lastSnapshotDropLogTime > 0.0 && now - _lastSnapshotDropLogTime < 5.0)
        {
            return;
        }

        Log.Warning(
            $"Deferred {_deferredSnapshotPackages} snapshot package(s) due to MTU budget. " +
            $"Last: {package.GetType().Name} ({packetSize}/{maxPacketSize} bytes)."
        );
        _deferredSnapshotPackages = 0;
        _lastSnapshotDropLogTime = now;
    }

    private static bool CanSendPackageToClient(
        OutboundPackage outboundPackage,
        Client client,
        bool checkClientState
    )
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

        return client.ConnectionPhase == ConnectionPhase.Live && client.State >= PackageManager.MinimumState(package);
    }
}
