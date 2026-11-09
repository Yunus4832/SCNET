using System.Collections.ObjectModel;
using System.Net;

using Engine.Network;
using Engine.Network.Security;

using EntitySystem.Core;

using Game.Admission;
using Game.Authentication;
using Game.Network.Chunks;
using Game.Network.Diagnostics;
using Game.Network.Handling;
using Game.Servers;
using Game.Terrains.Distribution;

using LiteNetLib.Utils;

namespace Game.Network;

public class NetNode
{
    internal NativeTerrainReceiver? NativeTerrainReception =>
        (Server?.Connection as NetworkMessageConnection)?.Receiver;

    public NetworkTerrainReceptionStatistics? CaptureTerrainReceptionStatistics()
    {
        var receiver = NativeTerrainReception;
        return receiver is null
            ? null
            : new NetworkTerrainReceptionStatistics(
                receiver.PendingTransferCount,
                receiver.PendingTransferBytes,
                receiver.FragmentsReceived,
                receiver.SnapshotsDecoded,
                receiver.FullChunkRequestsSent,
                receiver.MissingFragmentRequestsSent
            );
    }

    internal void QueueAdaptedNetworkPackages(
        Client recipient,
        OutboundPackage logical,
        IReadOnlyList<IProtocolMessage> messages
    )
    {
        var fanout = logical.Fanout ??
                     new NetworkMessageFanout(
                         SendStatistics,
                         logical.Package.GetType(),
                         PackageTransportPolicy.Get(logical.Package).Channel
                     );
        fanout.Advance(1, messages.Count);
        foreach (var message in messages)
        {
            if (!QueueNetworkPackage(recipient, logical with { Package = message, Fanout = fanout }))
            {
                fanout.Advance(1, 0);
            }
        }
    }

    internal void DiscardQueuedChunkRequests(ChunkAllocationId allocation)
    {
        void Trim(IReadOnlyList<OutboundPackage> pending, Action<int> remove, Action<int, OutboundPackage> replace)
        {
            for (var index = pending.Count - 1; index >= 0; index--)
            {
                var package = pending[index];
                var replacement = (IProtocolMessage?)package.Package;
                switch (package.Package)
                {
                    case TerrainChunksRequestPackage full:
                    {
                        var remaining = full.ChunkRequests.Where(request => request.Allocation != allocation).ToArray();
                        if (remaining.Length == full.ChunkRequests.Count) { continue; }

                        if (remaining.Length > 0) { replacement = full with { ChunkRequests = remaining }; }
                        else { replacement = null; }

                        break;
                    }
                    case TerrainFragmentsRequestPackage repair:
                    {
                        var remaining = repair.FragmentRequests.Where(request => request.Allocation != allocation)
                            .ToArray();
                        if (remaining.Length == repair.FragmentRequests.Count) { continue; }

                        if (remaining.Length > 0) { replacement = repair with { FragmentRequests = remaining }; }
                        else { replacement = null; }

                        break;
                    }
                    default: continue;
                }

                if (replacement == null)
                {
                    remove(index);
                    package.Fanout?.Advance(1, 0);
                }
                else { replace(index, package with { Package = replacement }); }
            }
        }

        lock (_pendingPackages)
        {
            Trim(_pendingPackages, _pendingPackages.RemoveAt, _pendingPackages.Replace);
            foreach (var (client, pending) in _networkPackages.ToArray())
            {
                Trim(pending, pending.RemoveAt, pending.Replace);
                if (pending.Count == 0) { _networkPackages.Remove(client); }
            }
        }
    }

    private NativeTerrainDelivery? _terrainDelivery;

    internal NativeTerrainDelivery TerrainDelivery => _terrainDelivery ??= new NativeTerrainDelivery(
        this,
        GetChunkDeliveryView,
        Options
    );

    public long DispatchCycle { get; private set; }

    public int OutstandingChunkEncodes => _terrainDelivery?.OutstandingEncodes ?? 0;

    private ChunkDeliveryView? GetChunkDeliveryView(Client client)
    {
        var scheduler = Runtime.Project?.FindSubsystem<SubsystemTerrain>()?.ChunkDistribution;
        return scheduler != null && scheduler.TryGetClientLocation(client, out var center, out var distance)
            ? new ChunkDeliveryView(center, scheduler.GetPredictedCenter(client, center), distance)
            : null;
    }

    internal bool IsCurrentChunkAllocation(Client client, ChunkAllocationId allocation) =>
        Runtime.Project?.FindSubsystem<SubsystemTerrain>()?.ChunkDistribution
            ?.IsCurrentAllocation(client, allocation) ==
        true;

    internal void DiscardOlderChunkAllocations(WorldConnection connection, ChunkAllocationId allocation)
    {
        var client = RequirePhysicalConnection(connection);
        DiscardQueuedSnapshots((package, message) => package.Audience.HasSameRecipients(PackageAudience.To(client)) &&
                                                     message.Snapshot.Allocation.Coords == allocation.Coords &&
                                                     message.Snapshot.Allocation.Generation < allocation.Generation
        );
        lock (_pendingPackages)
        {
            if (_networkPackages.TryGetValue(client, out var pending))
            {
                foreach (var package in pending.Where(package => package.Package is TerrainFragmentPackage fragment &&
                                                                 fragment.ChunkFragment.Allocation.Coords ==
                                                                 allocation.Coords &&
                                                                 fragment.ChunkFragment.Allocation.Generation <
                                                                 allocation.Generation
                         ).ToArray())
                {
                    pending.Remove(package);
                    package.Fanout?.Advance(1, 0);
                }
            }
        }

        _terrainDelivery?.DiscardOlderAllocations(client, allocation);
    }

    internal bool CanQueueTerrainSnapshot(WorldConnection client)
    {
        lock (_pendingPackages)
        {
            return !_isStopping &&
                   !Runtime.IsClosed &&
                   !ReferenceEquals(_logicalOverloadRuntime, Runtime) &&
                   !_logicalOverloadedRecipients.ContainsKey(client) &&
                   _pendingPackages.CountSnapshots(client) < 4 &&
                   _pendingPackages.CanReserve(
                       client,
                       128L + AuthorityChunkSnapshot.CellCount * 4L + AuthorityChunkSnapshot.ShaftCount * 8L
                   );
        }
    }

    internal bool TryQueueOwnedTerrainSnapshot(WorldConnection client, TerrainSnapshotMessage message)
    {
        lock (_pendingPackages)
        {
            if (_isStopping || !CanQueueTerrainSnapshot(client)) { return false; }

            // The authority scheduler captured these arrays before handing ownership to this queue.
            return _pendingPackages.TryEnqueue(new OutboundPackage(message, PackageAudience.To(client), Runtime));
        }
    }

    internal void QueueTerrainFragment(
        Client client,
        WorldRuntimeContext runtime,
        EncodedTerrainChunkFragment fragment,
        NetworkMessageFanout? parent
    )
    {
        var fanout = parent ??
                     new NetworkMessageFanout(
                         SendStatistics,
                         typeof(TerrainFragmentPackage),
                         PackageTransportPolicy.TerrainFragment.Channel
                     );
        if (parent != null) { parent.Advance(1, 2); }

        if (!QueueNetworkPackage(
                client,
                new OutboundPackage(
                    new TerrainFragmentPackage { ChunkFragment = fragment },
                    PackageAudience.To(client),
                    runtime
                ) { Fanout = fanout }
            ))
        {
            fanout.Advance(1, 0);
        }
    }

    internal void DiscardQueuedChunkFragments(Client client, ChunkAllocationId allocation)
    {
        lock (_pendingPackages)
        {
            if (!_networkPackages.TryGetValue(client, out var pending)) { return; }

            foreach (var package in pending.Where(package => package.Package is TerrainFragmentPackage fragment &&
                                                             fragment.ChunkFragment.Allocation == allocation
                     ).ToArray())
            {
                pending.Remove(package);
                package.Fanout?.Advance(1, 0);
            }

            if (pending.Count == 0) { _networkPackages.Remove(client); }
        }
    }

    private void DiscardQueuedSnapshots(Func<OutboundPackage, TerrainSnapshotMessage, bool> discard)
    {
        lock (_pendingPackages)
        {
            for (var index = _pendingPackages.Count - 1; index >= 0; index--)
            {
                var package = _pendingPackages[index];
                if (package.Package is not TerrainSnapshotMessage message || !discard(package, message)) { continue; }

                _pendingPackages.RemoveAt(index);
                package.Fanout?.Advance(1, 0);
            }
        }
    }

    internal void ReleaseChunkDelivery(WorldConnection connection)
    {
        if (!TryGetPhysicalConnection(connection, out var client) || client is null) { return; }

        DiscardQueuedSnapshots((package, _) => package.Audience.HasSameRecipients(PackageAudience.To(client)));
        _terrainDelivery?.RemoveClient(client);
    }

    internal void ReleaseConnectionDelivery(Client client)
    {
        ReleaseChunkDelivery(client);
        lock (_pendingPackages)
        {
            if (!_networkPackages.Remove(client, out var pending)) { return; }

            foreach (var package in pending) { package.Fanout?.Advance(1, 0); }

            pending.Clear();
        }
    }

    internal void TrimChunkDelivery(WorldConnection connection, Vector2 center, float distance)
    {
        if (!TryGetPhysicalConnection(connection, out var client) || client is null) { return; }

        DiscardQueuedSnapshots((package, message) => package.Audience.HasSameRecipients(PackageAudience.To(client)) &&
                                                     !NetworkTerrainPolicy.IsChunkCenterRelevant(
                                                         message.Snapshot.Allocation.Coords,
                                                         center,
                                                         distance
                                                     )
        );
        _terrainDelivery?.Trim(client);
    }

    internal void InvalidateChunkDelivery(Point2 coords)
    {
        DiscardQueuedSnapshots((_, message) => message.Snapshot.Allocation.Coords == coords);
        _terrainDelivery?.RemoveChunk(coords);
    }

    public TemporaryJoinAuthorization? TemporaryAuthentication { get; set; }

    public NetworkSendStatistics SendStatistics { get; } = new();

    public int PendingPackageCount
    {
        get
        {
            lock (_pendingPackages)
            {
                return _pendingPackages.BufferedCount + _networkPackages.Values.Sum(packages => packages.Count);
            }
        }
    }

    public long PendingLogicalPackageBytes
    {
        get
        {
            lock (_pendingPackages) { return _pendingPackages.PendingBytes; }
        }
    }

    public long PendingNetworkPackageBytes
    {
        get
        {
            lock (_pendingPackages) { return _pendingNetworkBytes; }
        }
    }

    public enum Stage
    {
        NotConnected = 0,

        Bootstrapping = 1,

        Connected = 2
    }


    private NetworkHost? _broadcastHost;

    private readonly Dictionary<Client, LocalizedText> _clientsToRemove = new(ReferenceEqualityComparer.Instance);

    private P2PRoomController _p2p = new();

    public P2PRoomSettings P2PConfiguration => _p2p.Configuration;

    public P2PRoomStatus GetP2PStatus() => new(
        _p2p.State,
        _p2p.DesiredEnabled,
        IsServer && _host is not null,
        IsServer && Runtime.Project is not null,
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

    public P2PRoomStartFailure StartConfiguredP2P()
    {
        var status = GetP2PStatus();
        if (!status.ListenerReady) { return P2PRoomStartFailure.ListenerRequired; }

        if (!status.WorldReady) { return P2PRoomStartFailure.WorldRequired; }

        if (status.State == P2PRoomState.Stopping) { return P2PRoomStartFailure.Stopping; }

        var failure = P2PRoomConfiguration.Validate(P2PConfiguration, out var configuration);
        if (failure != P2PRoomStartFailure.None) { return failure; }

        if (!status.DesiredEnabled) { StartP2P(configuration); }

        return P2PRoomStartFailure.None;
    }

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
    /// <summary>
    ///     包发送队列
    /// </summary>
    private readonly PendingLogicalMessages _pendingPackages = new();

    private WorldRuntimeContext? _logicalOverloadRuntime;

    private readonly Dictionary<WorldConnection, WorldRuntimeContext> _logicalOverloadedRecipients =
        new(ReferenceEqualityComparer.Instance);

    private const long _maximumPendingNetworkBytes = 128L * 1024 * 1024;

    private long _pendingNetworkBytes;

    private readonly Dictionary<Client, PendingNetworkMessages> _networkPackages =
        new(ReferenceEqualityComparer.Instance);

    private readonly Dictionary<(Client Client, NetworkChannel Channel), double> _lastPackageFlushTimes = new();

    private int _networkRecipientCursor;

    private int _deferredSnapshotPackages;

    private double _lastSnapshotDropLogTime;

    // 只有这些客户端同意以后新客户端才能加入
    public HashSet<byte> AgreeOnPendingPeer = [];

    public bool AllowHandle = true;

    private readonly Dictionary<int, Client> _clients = new();

    private readonly Dictionary<WorldConnection, Client> _physicalConnections = new(ReferenceEqualityComparer.Instance);

    private readonly ReadOnlyDictionary<int, Client> _clientsView;

    public IReadOnlyDictionary<int, Client> Clients => _clientsView;

    public WorldConnections Connections { get; private set; } = null!;

    internal Client RequirePhysicalConnection(WorldConnection connection)
    {
        return _physicalConnections.TryGetValue(connection, out var client)
            ? client
            : throw new InvalidOperationException("Logical connection is not registered with this physical endpoint.");
    }

    internal bool TryGetPhysicalConnection(WorldConnection connection, out Client? client)
    {
        return _physicalConnections.TryGetValue(connection, out client);
    }

    private bool _isStopping;

    private bool _preserveAdmissionOnStop;

    public event Func<bool>? AdmissionTransportInterrupted;

    public Action<WorldConnection>? OnClientStateChanged;

    public Action<Client>? OnClientTransportConnected;

    public Action<Client>? OnClientBootstrapApplied;

    public Action<Client>? OnClientBecameLive;

    private Client? _pendingClient;

    public NetworkConnection? PendingPeer => _pendingClient?.Peer;

    public Guid TokenId;

    private Client? _self;

    public Client? Self
    {
        get => _self;
        set
        {
            if (value is not null)
            {
                ObjectDisposedException.ThrowIf(Runtime.IsClosed, Runtime);
                ValidatePhysical(value);
            }

            var previous = _self;
            Connections.SetSelf(value);
            try
            {
                if (value is not null) { AttachPhysical(value); }

                _self = value;
            }
            catch
            {
                Connections.SetSelf(previous);
                throw;
            }

            DetachPhysicalIfUnused(previous);
        }
    }

    private Client? _server;

    public Client? Server
    {
        get => _server;
        set
        {
            if (value is not null)
            {
                ObjectDisposedException.ThrowIf(Runtime.IsClosed, Runtime);
                ValidatePhysical(value);
            }

            var previous = _server;
            Connections.SetAuthority(value);
            try
            {
                if (value is not null) { AttachPhysical(value); }

                _server = value;
            }
            catch
            {
                Connections.SetAuthority(previous);
                throw;
            }

            DetachPhysicalIfUnused(previous);
        }
    }

    public NodeStopReason StopReason { get; private set; } = NodeStopReason.None;

    public NetNodeOptions Options { get; }

    public Stage CurrentStage { get; set; } = Stage.NotConnected;

    private NodeRole _role = NodeRole.Inactive;

    private readonly TimeProvider _outboundTime;

    public bool ClientPresentation { get; }

    public NetNode(
        NetNodeOptions options,
        bool clientPresentation,
        PackageHandlerCatalog handlers,
        NodeRole initialRole
    ) : this(options, clientPresentation, handlers, initialRole, TimeProvider.System)
    {
    }

    internal NetNode(
        NetNodeOptions options,
        bool clientPresentation,
        PackageHandlerCatalog handlers,
        NodeRole initialRole,
        TimeProvider outboundTime
    )
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(outboundTime);
        if (!Enum.IsDefined(initialRole)) { throw new ArgumentOutOfRangeException(nameof(initialRole)); }

        _clientsView = new ReadOnlyDictionary<int, Client>(_clients);
        Options = options;
        BaseHandlers = handlers ?? throw new ArgumentNullException(nameof(handlers));
        ClientPresentation = clientPresentation;
        _outboundTime = outboundTime;
        _role = initialRole;
        Runtime = CreateRuntimeContext(initialRole == NodeRole.Authority ? RuntimeSide.Authority : RuntimeSide.Client);
    }

    public PackageHandlerCatalog BaseHandlers { get; }

    public PackageHandlerCatalog Handlers => WorldNetworkBinding.For(Runtime).Handlers;

    public WorldRuntimeContext Runtime { get; private set; }

    public NodeRole Role
    {
        get => _role;
        internal set
        {
            if (_role == value)
            {
                return;
            }

            _role = value;
            ResetRuntimeContext();
        }
    }

    private WorldRuntimeContext CreateRuntimeContext(RuntimeSide side)
    {
        var runtime = new WorldRuntimeContext(side, side == RuntimeSide.Client && ClientPresentation);
        Connections = new WorldConnections(runtime);
        _ = new WorldNetworkBinding(runtime, this, BaseHandlers);
        _ = new WorldCommandResults(runtime);
        if (!runtime.IsAuthority) { _ = new ClientBootstrapContent(runtime); }

        runtime.Lifetime.Register(() => DiscardOutbound(runtime));
        return runtime;
    }

    private void ResetRuntimeContext()
    {
        var previous = Runtime;
        _terrainDelivery?.Dispose();
        _terrainDelivery = null;
        ClearClientRegistrations();
        Self = null;
        Server = null;
        _physicalConnections.Clear();
        Runtime = CreateRuntimeContext(Role is NodeRole.Authority ? RuntimeSide.Authority : RuntimeSide.Client);
        try { previous.Close(); }
        finally { DiscardOutbound(previous); }
    }

    private bool IsCurrentOutbound(OutboundPackage package)
    {
        return ReferenceEquals(package.Runtime, Runtime) && !package.Runtime.IsClosed;
    }

    private void DiscardOutbound(WorldRuntimeContext source)
    {
        lock (_pendingPackages)
        {
            _lastPackageFlushTimes.Clear();
            _networkRecipientCursor = 0;
            _pendingPackages.Discard(source);
            if (ReferenceEquals(_logicalOverloadRuntime, source)) { _logicalOverloadRuntime = null; }

            foreach (var client in _logicalOverloadedRecipients.Where(pair => ReferenceEquals(pair.Value, source))
                         .Select(pair => pair.Key).ToArray())
            {
                _logicalOverloadedRecipients.Remove(client);
            }

            foreach (var (client, packages) in _networkPackages.ToArray())
            {
                for (var index = packages.Count - 1; index >= 0; index--)
                {
                    if (!ReferenceEquals(packages[index].Runtime, source)) { continue; }

                    packages[index].Fanout?.Advance(1, 0);
                    packages.RemoveAt(index);
                }

                if (packages.Count == 0) { _networkPackages.Remove(client); }
            }
        }
    }

    public PackageReceiveContext CreateReceiveContext(Client? sender = null) => new(this, Runtime, sender);

    public WorldRuntimeContext PrepareWorldRuntime()
    {
        if (Runtime.IsClosed)
        {
            ResetRuntimeContext();
        }

        if (Runtime.Project is not null)
        {
            throw new InvalidOperationException("Release the previous world before creating another one.");
        }

        return Runtime;
    }

    public void ReleaseProject(Project project)
    {
        if (ReferenceEquals(Runtime.Project, project))
        {
            ResetRuntimeContext();
        }
    }

    public bool IsConnected => CurrentStage == Stage.Connected;

    public bool IsServer => Role == NodeRole.Authority;

    private LocalPlayerAdmission? _localAdmission;

    private Guid _localAdmissionToken;


    public void BeginLocalAdmission()
    {
        if (!IsServer ||
            _host is null ||
            Runtime.Project is not { } world ||
            AdmissionController is not { } controller ||
            !ReferenceEquals(controller.World, world) ||
            Self?.PlayerAddress is not null)
        {
            throw new InvalidOperationException("Start the server world before joining a local player.");
        }

        LocalAdmissionError = null;
        _verifiedLocalPlayer = null;
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

    public LocalizedText? LocalAdmissionError { get; private set; }

    public bool HasPendingAdmission => _localAdmission is not null || CurrentStage == Stage.Bootstrapping;

    public void EndAdmission(string error = "") => EndAdmission(LocalizedText.Literal(error));

    public void EndAdmission(LocalizedText error)
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

    private PlayerAddress? _verifiedLocalPlayer;

    public (PlayerAddress Subject, Guid Token) ConsumeLocalAdmission()
    {
        if (!AdmissionAccepted || _verifiedLocalPlayer is not { } subject)
        {
            throw new InvalidOperationException("No verified local admission is available.");
        }

        _verifiedLocalPlayer = null;
        var token = _localAdmissionToken;
        _localAdmissionToken = Guid.Empty;
        return (subject, token);
    }

    public void PumpLocalAdmission()
    {
        if (_isStopping || _localAdmission is null) { return; }

        if (!ReferenceEquals(_localAdmission.World, Runtime.Project))
        {
            EndAdmission(ConnectionMessages.Create("WorldChanged", "The world has changed. Please rejoin."));
            return;
        }

        if (_localAdmission.Poll(DateTimeOffset.UtcNow) is not { } result) { return; }

        if (result.Code == AdmissionResultCode.Accepted)
        {
            _verifiedLocalPlayer = new PlayerAddress(_localAdmission.Challenge.Subject);
        }

        ApplyAdmissionResult(result);
        if (result.Code == AdmissionResultCode.Accepted) { _localAdmission = null; }
    }

    public bool IsClosing => _isStopping;

    internal ServerAdmissionController? AdmissionController { get; private set; }

    public ServerAdmissionPolicy? AdmissionPolicy => AdmissionController?.Policy;

    public AdmissionChallengePackage? PendingAdmissionChallenge { get; private set; }

    public DateTimeOffset? AdmissionDeadline { get; private set; }

    public bool AdmissionAccepted { get; private set; }

    public bool IsTransportConnecting => _connecting is { IsCompleted: false };

    private ReceivedPackage? _admissionBootstrap;

    public event Action<AdmissionChallengePackage>? AdmissionRequired;

    public event Action<AdmissionResultPackage>? AdmissionResultReceived;

    private NetworkConnection? _admissionServerPeer;


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
                client = new Client(peer) { Node = this };
                client.Connection = new NetworkMessageConnection(this, client, peer);
                _transportClients.Add(peer.Id, client);
            }

            return client;
        }

        return request is not null ? new Client(request) : endpoint is not null ? new Client(endpoint) : null;
    }

    internal Client? PendingPlayer => PendingClient;

    private Client? PendingClient => _pendingClient;

    public IEnumerable<Client> Peers => Clients.Values.Where(client => client.Peer is not null);

    // 当一个客户端收到一个消息
    public event Action<NetNode, IEnumerable<ReceivedPackage>>? OnReceive;

    //添加到队列
    public void QueuePackage(IProtocolMessage package, PackageAudience audience)
    {
        QueuePackage(Runtime, package, audience);
    }

    internal void QueuePackage(WorldRuntimeContext source, IProtocolMessage package, PackageAudience audience)
    {
        if (source.IsClosed || _isStopping) { return; }

        if (!ReferenceEquals(WorldNetworkBinding.FindNode(source), this))
        {
            throw new InvalidOperationException("An outbound message must belong to this endpoint.");
        }

        if (_isStopping || !ReferenceEquals(source, Runtime) || source.IsClosed) { return; }

        var owned = package is TerrainSnapshotMessage snapshot
            ? snapshot.Capture()
            : ProtocolMessageOwnership.Capture(package);
        QueueOutboundPackage(new OutboundPackage(owned, audience, source));
    }

    private void QueueOutboundPackage(OutboundPackage package, long? sequence = null)
    {
        lock (_pendingPackages)
        {
            var recipient = package.Audience.SingleRecipient;
            if (_isStopping ||
                !IsCurrentOutbound(package) ||
                ReferenceEquals(_logicalOverloadRuntime, package.Runtime) ||
                recipient is not null && _logicalOverloadedRecipients.ContainsKey(recipient))
            {
                package.Fanout?.Advance(1, 0);
                return;
            }

            if (_pendingPackages.TryEnqueue(package, sequence)) { return; }

            package.Fanout?.Advance(1, 0);
            if (recipient is not null)
            {
                Log.Warning(
                    $"Logical recipient queue overloaded: type={package.Package.GetType().Name}, " +
                    $"pending={_pendingPackages.BufferedCount}, bytes={_pendingPackages.PendingBytes}, " +
                    $"recipient={recipient.ID}."
                );
                _logicalOverloadedRecipients[recipient] = package.Runtime;
                _pendingPackages.DiscardRecipient(recipient);
            }
            else { _logicalOverloadRuntime = package.Runtime; }
        }
    }

    private bool IsLogicalRecipientOverloaded(Client client)
    {
        lock (_pendingPackages) { return _logicalOverloadedRecipients.ContainsKey(client); }
    }

    private void ApplyLogicalOverload()
    {
        KeyValuePair<WorldConnection, WorldRuntimeContext>[] recipients;
        bool stop;
        lock (_pendingPackages)
        {
            stop = ReferenceEquals(_logicalOverloadRuntime, Runtime);
            recipients = _logicalOverloadedRecipients.ToArray();
        }

        if (stop)
        {
            Stop(
                ConnectionMessages.Create(
                    "PublicationQueueFull",
                    "The logical publication queue cannot accept another message."
                )
            );
        }

        foreach (var (client, runtime) in recipients)
        {
            if (ReferenceEquals(runtime, Runtime))
            {
                if (TryGetPhysicalConnection(client, out var physical))
                {
                    RemoveClient(
                        physical,
                        ConnectionMessages.Create(
                            "LogicalQueueFull",
                            "The logical recipient queue cannot accept another message."
                        )
                    );
                }
            }
        }
    }

    internal bool QueueNetworkPackage(Client client, OutboundPackage package, bool prepend = false)
    {
        lock (_pendingPackages)
        {
            if (_isStopping ||
                !client.IsConnected ||
                _clientsToRemove.ContainsKey(client) ||
                !IsCurrentOutbound(package))
            {
                return false;
            }

            package = package with { Audience = PackageAudience.To(client) };
            if (!_networkPackages.TryGetValue(client, out var pending))
            {
                pending = new PendingNetworkMessages(bytesChanged: change => _pendingNetworkBytes += change);
                _networkPackages.Add(client, pending);
            }

            // A queued snapshot was published after this retry and already supersedes it.
            if (prepend && pending.HasCoalescibleMessage(package)) { return false; }

            var change = pending.EstimateChange(package);
            if (_pendingNetworkBytes > _maximumPendingNetworkBytes - change || !pending.TryEnqueue(package, prepend))
            {
                foreach (var discarded in pending) { discarded.Fanout?.Advance(1, 0); }

                pending.Clear();
                _networkPackages.Remove(client);
                RemoveClient(
                    client,
                    ConnectionMessages.Create(
                        "OutgoingQueueFull",
                        "The outgoing message queue cannot accept another message."
                    )
                );
                return false;
            }

            return true;
        }
    }

    private byte FindUnusedIndex()
    {
        for (byte i = 1; i < 255; i++)
        {
            if (Clients.ContainsKey(i))
            {
                continue;
            }

            return i;
        }

        throw new InvalidOperationException("The server is full.");
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

        var client = ResolveSender(peer: peer)!;
        client.AssignRoutingId(FindUnusedIndex());
        client.TokenId = tokenId;
        client.SetControlledCharacter(guid);
        return client;
    }

    internal bool CanReceiveFromSession(Client? sender, IProtocolMessage package)
    {
        return sender is not null &&
               !_isStopping &&
               !_clientsToRemove.ContainsKey(sender) &&
               (Clients.TryGetValue(sender.ID, out var registered) && ReferenceEquals(registered, sender) ||
                package is AdmissionProofPackage);
    }

    internal void ReceiveAdmissionChallenge(Client sender, AdmissionChallengePackage challenge)
    {
        if (IsServer || CurrentStage != Stage.Bootstrapping || sender.Peer is not { } peer) { return; }

        if (AdmissionAccepted)
        {
            Stop(
                ConnectionMessages.Create(
                    "ChallengeChangedAfterAdmission",
                    "The server changed the challenge after authentication"
                )
            );
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
            Stop(ConnectionMessages.Create("ChallengeInvalid", "The authentication challenge is expired or invalid"));
            return;
        }

        if (PendingAdmissionChallenge is { } pending)
        {
            if (ReferenceEquals(_admissionServerPeer, peer) && pending == challenge) { return; }

            Stop(ConnectionMessages.Create("ChallengeChanged", "The server changed the authentication challenge"));
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

    public string? AdmissionServerAddress =>
        _localAdmission?.Challenge.Server ?? _admissionServerPeer?.PeerIdentity.Address;

    public SCNET.Authentication.Protocol.Contracts.AdmissionChallenge CaptureAdmissionChallenge()
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

    public bool CanSubmitAdmission(string nonce)
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
            AdmissionDeadline = DateTimeOffset.UtcNow.AddMilliseconds(
                NetworkTransportLimits.DisconnectTimeoutMilliseconds
            );
        }

        AdmissionResultReceived?.Invoke(result);
        if (result.Code == AdmissionResultCode.Accepted && !_isStopping && _admissionBootstrap is { } bootstrap)
        {
            _admissionBootstrap = null;
            AddPendingHandlePackage(bootstrap);
        }

        if (!_isStopping &&
            result.Code is AdmissionResultCode.Cancelled or AdmissionResultCode.Expired or AdmissionResultCode.Denied)
        {
            EndAdmission(
                ConnectionMessages.Create("AdmissionEnded", "Authentication was cancelled, expired or denied")
            );
        }
    }

    private void ValidatePhysical(Client client)
    {
        if (client.Node is not null && !ReferenceEquals(client.Node, this))
        {
            throw new InvalidOperationException("A connection cannot be shared between runtime endpoints.");
        }

        if (client.World is not null && !ReferenceEquals(client.World, Runtime))
        {
            throw new InvalidOperationException("A connection belongs to another world.");
        }
    }

    private void AttachPhysical(Client client)
    {
        ValidatePhysical(client);
        client.Node = this;
        if (client.Peer is { } peer && client.Connection is null)
        {
            client.Connection = new NetworkMessageConnection(this, client, peer);
        }

        _physicalConnections[client] = client;
    }

    private void DetachPhysicalIfUnused(Client? client)
    {
        if (client is not null &&
            !ReferenceEquals(client, _self) &&
            !ReferenceEquals(client, _server) &&
            !_clients.Values.Any(member => ReferenceEquals(member, client)))
        {
            _physicalConnections.Remove(client);
            if (ReferenceEquals(client.Node, this)) { client.Node = null; }
        }
    }

    public void AddClient(Client client)
    {
        if (Runtime.IsClosed) { throw new ObjectDisposedException(nameof(Runtime)); }

        ValidatePhysical(client);
        if (_clients.TryGetValue(client.ID, out var previous) && !ReferenceEquals(previous, client))
        {
            UnregisterClient(previous);
        }

        AttachPhysical(client);
        try
        {
            Connections.Register(client);
            _clients[client.ID] = client;
            if (client.Peer is not null && (IsServer || client.ID == 0))
            {
                _transportClients[client.Peer.Id] = client;
            }
        }
        catch
        {
            Connections.Remove(client);
            DetachPhysicalIfUnused(client);
            throw;
        }
    }

    public bool UnregisterClient(Client client)
    {
        if (!_clients.TryGetValue(client.ID, out var registered) || !ReferenceEquals(registered, client))
        {
            return false;
        }

        _clients.Remove(client.ID);
        Connections.Remove(client);
        if (client.Peer is { } peer &&
            _transportClients.TryGetValue(peer.Id, out var transport) &&
            ReferenceEquals(transport, client))
        {
            _transportClients.Remove(peer.Id);
        }

        lock (_pendingPackages) { _pendingPackages.DiscardRecipient(client); }

        DetachPhysicalIfUnused(client);
        return true;
    }

    private void ClearClientRegistrations()
    {
        foreach (var client in _clients.Values.ToArray()) { UnregisterClient(client); }
    }

    public void RemoveClient(Client? client, string reason = "") => RemoveClient(client, LocalizedText.Literal(reason));

    public void RemoveClient(Client? client, LocalizedText reason)
    {
        if (client is null)
        {
            return;
        }

        if (_clientsToRemove.TryAdd(client, reason))
        {
            return;
        }

        if (reason != LocalizedText.Empty)
        {
            _clientsToRemove[client] = _clientsToRemove[client] == LocalizedText.Empty
                ? reason
                : LocalizedText.Format(LocalizedText.Literal("{0}\n{1}"), _clientsToRemove[client], reason);
        }
    }

    // Explicit StopImmediate callers own their transition; deferred stops notify the world owner.
    public event Action<NetNode, WorldRuntimeContext, NodeRole, NodeStopReason>? Stopped;

    public void RemoveClientImmediate(Client client, string reason = "") =>
        RemoveClientImmediate(client, LocalizedText.Literal(reason));

    public void RemoveClientImmediate(Client client, LocalizedText reason)
    {
        lock (_pendingPackages)
        {
            foreach (var key in _lastPackageFlushTimes.Keys.Where(key => ReferenceEquals(key.Client, client)).ToArray())
            {
                _lastPackageFlushTimes.Remove(key);
            }
        }

        lock (_pendingPackages)
        {
            _logicalOverloadedRecipients.Remove(client);
            _pendingPackages.DiscardRecipient(client);
        }

        var localPlayer = ReferenceEquals(client, Self);
        if (localPlayer && !IsServer)
        {
            Stop(reason);
            return;
        }

        if (localPlayer) { Self = null; }

        client.Connection?.Disconnect(reason);

        if (client == Server)
        {
            Stop();
            CurrentStage = Stage.NotConnected;
            return;
        }

        if (IsServer && client.PlayerAddress is { } playerAddress)
        {
            Runtime.Project?.FindSubsystem<SubsystemTeams>()?.CancelPendingOperations(playerAddress);
        }

        if (Clients.TryGetValue(client.ID, out var registered) && ReferenceEquals(registered, client))
        {
            if (IsServer)
            {
                QueuePackage(
                    new ClientRemovedPackage
                    {
                        ClientId = client.ID, ConnectionEpoch = client.ConnectionEpoch,
                        ConnectionGeneration = client.ConnectionGeneration
                    },
                    PackageAudience.Global
                );
            }

            client.State = ClientState.NotConnected;
            OnClientStateChanged?.Invoke(client);
            UnregisterClient(client);
        }
    }

    /// <summary>
    ///     服务器关闭，不执行OnClientStateChanged，防止多次Dispose
    /// </summary>
    public void RemoveAllClients(string reason = "") => RemoveAllClients(LocalizedText.Literal(reason));

    public void RemoveAllClients(LocalizedText reason)
    {
        var clients = new List<Client>(Clients.Values);
        foreach (var client in clients)
        {
            if (IsServer && client.PlayerAddress is { } playerAddress)
            {
                Runtime.Project?.FindSubsystem<SubsystemTeams>()?.CancelPendingOperations(playerAddress);
            }

            if (client == Self)
            {
                continue;
            }

            client.Connection?.Disconnect(reason);

            UnregisterClient(client);
            client.State = ClientState.NotConnected;
        }
    }


    private void HandleDatagram(NetworkDatagramEndpoint source, byte[] payload)
    {
        UnconnectedReceiveEvent(source.TransportEndpoint, payload);
    }

    private void HandleBroadcastDatagram(NetworkDatagramEndpoint source, byte[] payload)
    {
        UnconnectedReceiveEvent(source.TransportEndpoint, payload);
    }

    /// <summary>
    ///     连接断开事件 服务端
    /// </summary>
    private void PeerDisconnectedEvent(NetworkConnection peer, NetworkDisconnectInfo disconnectInfo)
    {
        AdmissionController?.Forget(peer);
        Log.Information($"{peer.TransportEndpoint}连接关闭: " + disconnectInfo.Reason);
        var str = DecodeDisconnectReason(disconnectInfo);

        if (Role == NodeRole.Client)
        {
            Log.Debug(
                $"Client transport disconnected: stage={CurrentStage}, admitted={AdmissionAccepted}, admissionOwner={AdmissionTransportInterrupted is not null}."
            );
            if (CurrentStage == Stage.Bootstrapping &&
                !AdmissionAccepted &&
                AdmissionTransportInterrupted?.Invoke() == true)
            {
                _preserveAdmissionOnStop = true;
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

            RemoveClientImmediate(client);

            //同步Client列表
            if (peer == PendingPeer)
            {
                _pendingClient = null;
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
    private void UnconnectedReceiveEvent(IPEndPoint remoteEndPoint, ReadOnlyMemory<byte> payload)
    {
        if (CurrentStage == Stage.NotConnected)
        {
            return;
        }

        var list = ProtocolReceivePolicy.DecodePackages(
            CreateReceiveContext(ResolveSender(endpoint: remoteEndPoint)),
            payload
        );
        foreach (var packageItem in list)
        {
            if (packageItem.Package is not ServerInfoPackage) { continue; }

            try
            {
                WorldNetworkBinding.For(packageItem.Context.Runtime).Dispatch(packageItem);
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
        if (package.Context.Runtime.IsClosed)
        {
            (package.Package as IDisposable)?.Dispose();
            return;
        }

        if (!WorldNetworkBinding.For(package.Context.Runtime).Inbox.TryPostOwned(package) &&
            !package.Context.Runtime.IsClosed)
        {
            throw new InvalidOperationException("The runtime message inbox is full.");
        }
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
        var list = ProtocolReceivePolicy.DecodePackages(CreateReceiveContext(fromClient), message.Payload);
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

        foreach (var received in list)
        {
            var native = fromClient?.Connection as NetworkMessageConnection;
            if (!TryQueueReceivedNetworkMessage(received, native)) { return; }
        }
    }

    internal bool TryQueueReceivedNetworkMessage(ReceivedPackage received, NetworkMessageConnection? native)
    {
        ReceivedPackage? normalized = null;
        try
        {
            normalized = native != null ? native.NormalizeIncoming(received) : received;
            if (normalized is null) { return true; }

            if (received.Context.Runtime.IsClosed)
            {
                var expiredPayload = normalized.Value.Package;
                normalized = null;
                (expiredPayload as IDisposable)?.Dispose();
                return true;
            }

            if (WorldNetworkBinding.For(received.Context.Runtime).Inbox
                .TryPostOwned(normalized.Value, native?.Lifetime ?? default))
            {
                return true;
            }

            var rejectedPayload = normalized.Value.Package;
            normalized = null;
            (rejectedPayload as IDisposable)?.Dispose();
            if (rejectedPayload is TerrainSnapshotMessage && native != null) { return true; }

            RemoveClient(
                received.Context.Sender,
                ConnectionMessages.Create("InboxFull", "The runtime message inbox is full.")
            );
        }
        catch (Exception exception)
        {
            (normalized?.Package as IDisposable)?.Dispose();
            Log.Error(exception);
            RemoveClient(
                received.Context.Sender,
                ConnectionMessages.Create("NativeDeliveryFailed", "Error on native message delivery.")
            );
        }

        return false;
    }

    public int PumpReceivedPackages(int maximumMessages = 256)
    {
        if (OnReceive is not { } receive)
        {
            return 0;
        }

        return WorldNetworkBinding.For(Runtime).Inbox.Drain(
            maximumMessages,
            message =>
            {
                if (message.Context.IsServer &&
                    (!CanReceiveFromSession(message.Context.Sender, message.Package) ||
                     !ProtocolReceivePolicy.CanHandleFromClient(message.Package, message.Context.Sender!)))
                {
                    return;
                }

                try
                {
                    receive(this, [message]);
                }
                catch (Exception exception)
                {
                    Log.Error(exception);
                    RemoveClient(
                        message.Context.Sender,
                        ConnectionMessages.Create(
                            "MessageDeliveryFailed",
                            "Message delivery failed: {0}",
                            exception.Message
                        )
                    );
                }
            }
        );
    }

    /// <summary>
    ///     新客户端连接事件
    /// </summary>
    /// <param name="request"></param>
    private void ConnectionRequestEvent(NetworkConnectionRequest request)
    {
        Log.Information("接收到连接请求 " + request.TransportEndpoint);

        if (Runtime.Project == null)
        {
            SendWriterFromPackage(
                new ConnectionRejectPackage(
                    ConnectionMessages.Create("ServerStarting", "Please wait for the server to finish starting")
                ),
                request,
                true
            );
        }
        else if (PendingClient != null)
        {
            Log.Information($"Too Many Request And Reject Current Request:{request.TransportEndpoint}");
            Log.Information($"Current Pending Client:{PendingClient}");
            SendWriterFromPackage(
                new ConnectionRejectPackage(
                    ConnectionMessages.Create(
                        "TooManyRequests",
                        "Too many connection requests. Please try again later."
                    )
                ),
                request,
                true
            );
        }
        else
        {
            try
            {
                var requestPackage =
                    ProtocolReceivePolicy.DecodePackage<ConnectionRequestPackage>(
                        CreateReceiveContext(ResolveSender(request: request)),
                        request.Handshake
                    );
                WorldNetworkBinding.For(requestPackage.Context.Runtime).Dispatch(
                    requestPackage.Package,
                    requestPackage.Context
                );
            }
            catch (Exception e)
            {
                SendWriterFromPackage(
                    new ConnectionRejectPackage(
                        ConnectionMessages.Create("InvalidRequest", "Invalid connection request")
                    ),
                    request,
                    true
                );
                Log.Error($"ConnectionRequestEvent Error: {e}");
            }
        }
    }

    public void PrepareAuthorityWorld()
    {
        if (Runtime.BoundProject is { IsDisposed: false })
        {
            throw new InvalidOperationException("Release the current world before preparing an authority runtime.");
        }

        Role = NodeRole.Authority;
        TemporaryAuthentication = null;
        _isStopping = false;
        ClearClientRegistrations();
        Self = null;
        Server = null;
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
                DisconnectTimeout = TimeSpan.FromMilliseconds(NetworkTransportLimits.DisconnectTimeoutMilliseconds)
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
            var project = Runtime.Project ??
                          throw new InvalidOperationException("Load the admitted world before starting its listener.");
            var policy = policyOverride ??
                         ServerAdmissionPolicy.Load(project.ExtensionData.Get("game", "admission"));
            using (var identity = LocalIdentityManager.Current.OpenServerIdentity(serverAddress))
            {
                if (!Runtime.IsAuthority || Role != NodeRole.Authority)
                {
                    throw new InvalidOperationException(
                        "Prepare the authoritative server world before opening its listener."
                    );
                }

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
            CurrentStage = Stage.Connected;
            Role = NodeRole.Authority;
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

    internal static ConnectionRequestPackage CreateConnectionRequest(Guid tokenId, ClientConnectionOptions options) =>
        new(tokenId, GameProtocol.CurrentVersion, options.ModDataHash);

    public void ConnectServer(ServerConnectionPreparation prepared, ClientConnectionOptions options)
    {
        if (_host is not null)
        {
            throw new InvalidOperationException("连接已启动");
        }

        ArgumentNullException.ThrowIfNull(options);
        var transport = prepared.Consume();
        TemporaryAuthentication = null;
        try
        {
            _isStopping = false;
            Log.Information("Connecting to prepared server");
            ClearClientRegistrations();
            _host = transport.Host;
            _preparedPath = transport.Path;
            RemoteTarget = prepared.Target;
            AttachHostEvents(_host);
            _connectingCancellation = new CancellationTokenSource();
            TokenId = Guid.NewGuid();
            var request = CreateConnectionRequest(TokenId, options);
            CurrentStage = Stage.Bootstrapping;
            Role = NodeRole.Client;
            _connecting = _preparedPath.ConnectAsync(CreateUnconnectedPayload(request), _connectingCancellation.Token);
        }
        catch (Exception e)
        {
            StopReason = new NodeStopReason(e.Message);
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

        var admitted = PendingClient;
        _pendingClient = null;
        AddClient(admitted);
        admitted.ConnectionPhase = ConnectionPhase.TransportConnected;
        OnClientTransportConnected?.Invoke(admitted);
        Log.Debug($"Client[{admitted.ID}]传输连接完成，开始Bootstrap");
    }

    public void BeginClientBootstrap(Client client)
    {
        if (!Runtime.IsAuthority || _pendingClient is not null || client.Node != this)
        {
            throw new InvalidOperationException(
                "A client bootstrap requires an admitted session on an available authority."
            );
        }

        Connections.BeginConnectionLifetime(client);
        _pendingClient = client;
        var notification = new ClientAddedPackage { Client = ClientPackageBuilder.Capture(client) };
        foreach (var other in Clients.Values.Where(other => other.IsConnected && other.PlayerAddress is not null))
        {
            if (other.Connection is { IsInProcess: true })
            {
                QueuePackage(notification, PackageAudience.To(other));
            }
            else if (other.Peer is { } peer)
            {
                AgreeOnPendingPeer.Add(other.ID);
                SendWriterFromPackage(notification, peer, true);
            }
        }

        DeliveryEvent(null);
    }

    public Client AdmitInProcessPlayer(PlayerAddress address, Guid token)
    {
        if (!IsServer || Runtime.Project is not { } world || Self is not null || PendingClient is not null)
        {
            throw new InvalidOperationException(
                "Prepare an independent authority world before admitting its local client."
            );
        }

        if (!PlayerAdmission.TryCreateSession(
                world,
                this,
                address,
                token,
                character => new Client(null, FindUnusedIndex(), token, character)
                    { PlayerAddress = address, Node = this },
                out var admitted,
                out var error
            ))
        {
            throw new InvalidOperationException(error?.Fallback);
        }

        _isStopping = false;
        CurrentStage = Stage.Connected;
        return admitted!;
    }

    public void PrepareInProcessClient(Client authority, Client self)
    {
        if (Runtime.IsAuthority || Runtime.HasProjectResources || Self is not null || Server is not null)
        {
            throw new InvalidOperationException("Use an unused client endpoint for an in-process join.");
        }

        Role = NodeRole.Client;
        TokenId = self.TokenId;
        AdmissionAccepted = true;
        Self = self;
        Server = authority;
        authority.Node = this;
        AddClient(self);
        CurrentStage = Stage.Bootstrapping;
    }

    public void Stop(string message = "") => Stop(new NodeStopReason(message));

    public void Stop(LocalizedText message) => Stop(new NodeStopReason(message));

    private void Stop(NodeStopReason reason)
    {
        TemporaryAuthentication = null;
        _isStopping = true;
        StopReason = reason;
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

    public void StopForAdmissionRecovery()
    {
        if (AdmissionAccepted || CurrentStage == Stage.Connected)
        {
            throw new InvalidOperationException("An admitted world session cannot resume a pending authentication.");
        }

        StopImmediate();
    }

    public void StopImmediate()
    {
        TemporaryAuthentication = null;
        _verifiedLocalPlayer = null;
        _isStopping = true;
        try
        {
            _pendingClient = null;
            AgreeOnPendingPeer.Clear();
            lock (_pendingPackages)
            {
                foreach (var outboundPackage in _pendingPackages)
                {
                    outboundPackage.Fanout?.Advance(1, 0);
                }

                _pendingPackages.Clear();
                foreach (var pending in _networkPackages.Values)
                {
                    foreach (var package in pending) { package.Fanout?.Advance(1, 0); }

                    pending.Clear();
                }

                _networkPackages.Clear();
            }

            _lastPackageFlushTimes.Clear();
            StopHost(ref _broadcastHost);

            // 先发送提示，然后再关闭服务器
            if (Role == NodeRole.Authority)
            {
                RemoveAllClients(ConnectionMessages.Create("ServerClosed", "The server closed the connection"));
            }

            _ = _p2p.ShutdownAsync();
            _p2p = new P2PRoomController();
            _connectingCancellation?.Cancel();
            foreach (var transport in _transportClients.Values.ToArray()) { transport.Connection?.Dispose(); }

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
            OnReceive = null;
            OnClientStateChanged = null;
            OnClientTransportConnected = null;
            OnClientBootstrapApplied = null;
            OnClientBecameLive = null;


            CurrentStage = Stage.NotConnected;
            Role = NodeRole.Inactive;
            ResetRuntimeContext();
            _preserveAdmissionOnStop = false;
            ClearClientRegistrations();
            Self = null;
            Server = null;
        }
    }

    /// <summary>
    ///     开启包处理
    /// </summary>
    public void TurnOnPackageHandle(Project project)
    {
        Runtime.BindProject(project);
        OnReceive += (node, list) =>
        {
            foreach (var c in list)
            {
                try
                {
                    WorldNetworkBinding.For(c.Context.Runtime).Dispatch(c);
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
                    : CreateUnconnectedPayload(
                        new ConnectionRejectPackage(
                            ConnectionMessages.Create("RequestRejected", "Connection request rejected")
                        )
                    )
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

    public static byte[] CreateUnconnectedPayload(IProtocolMessage package)
    {
        return ProtocolPacketCodec.Encode([package]);
    }

    public void Update()
    {
        // 先处理UI操作，Inventory序号增加后再处理客户端过来的包
        // 发送物品同步数据
        Runtime.Project?.FindSubsystem<SubsystemInventories>()?.FlushSyncItems();

        if (!_isStopping &&
            !Runtime.IsAuthority &&
            Server?.Connection is InProcessMessageConnection { IsConnected: false } localConnection)
        {
            Stop(localConnection.DisconnectReason);
        }

        if (!_isStopping)
        {
            foreach (var client in Clients.Values.Where(client => client.Connection is not null &&
                                                                  !client.IsConnected &&
                                                                  client.State >= ClientState.Connected
                     ).ToArray())
            {
                RemoveClient(client, ConnectionMessages.Create("Closed", "Connection closed"));
            }
        }

        var removals = _clientsToRemove.ToArray();
        _clientsToRemove.Clear();
        foreach (var pair in removals)
        {
            RemoveClientImmediate(pair.Key, pair.Value);
        }

        _terrainDelivery?.Update(Time.FrameStartTime);

        // 批处理发送包队列，按传输通道分流，避免可靠大包阻塞实时状态。
        FlushPendingPackages();
        DispatchCycle = checked(DispatchCycle + 1);

        if (_isStopping)
        {
            var previousRole = Role;
            var stoppedRuntime = Runtime;
            var error = StopReason;
            var preserveAdmission = _preserveAdmissionOnStop;
            StopImmediate();
            if (!preserveAdmission && previousRole != NodeRole.Inactive)
            {
                Stopped?.Invoke(this, stoppedRuntime, previousRole, error);
            }
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
            Runtime.Project is { } project)
        {
            _p2p.ConsumeAutomaticRequest();
            var automaticFailure = StartConfiguredP2P();
            if (automaticFailure != P2PRoomStartFailure.None)
            {
                Log.Warning($"Could not automatically start P2P room: {automaticFailure}");
            }
        }

        _host?.DispatchEvents();
        PumpReceivedPackages();
        if (_pendingClient is { Connection: { IsConnected: false } } cancelled)
        {
            _pendingClient = null;
            AgreeOnPendingPeer.Clear();
            cancelled.Connection.Dispose();
        }

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
                RemoveClient(
                    receipt.Client,
                    ConnectionMessages.Create("DeliveryFailed", "Message delivery confirmation failed")
                );
            }
        }
    }

    internal static byte[] EncodeDisconnectReason(string reason) =>
        EncodeDisconnectReason(LocalizedText.Literal(reason));

    internal static byte[] EncodeDisconnectReason(LocalizedText reason)
    {
        try
        {
            var encoded = ProtocolPacketCodec.Encode([new ConnectionRejectPackage(reason)], CompressionPolicy.None);
            if (encoded.Length <= 256) { return encoded; }
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidDataException
                                              or MessagePack.MessagePackSerializationException)
        {
            // Oversized application detail must not prevent the transport from closing.
        }

        return CreateUnconnectedPayload(
            new ConnectionRejectPackage(ConnectionMessages.Create("Closed", "Connection closed"))
        );
    }

    internal static NodeStopReason DecodeDisconnectReason(NetworkDisconnectInfo information)
    {
        if (!information.Data.IsEmpty)
        {
            try
            {
                if (information.Reason is NetworkDisconnectReason.Rejected or NetworkDisconnectReason.RemoteClose &&
                    ProtocolPacketCodec.Decode(information.Data) is [ConnectionRejectPackage reason])
                {
                    return new NodeStopReason(reason.Reason, information.Reason);
                }
            }
            catch (Exception exception) when (exception is InvalidDataException or ArgumentException
                                                  or EndOfStreamException)
            {
                // Preserve the transport fact when optional remote text is malformed.
            }
        }

        return new NodeStopReason(string.Empty, information.Reason);
    }

    private void FlushPendingPackages()
    {
        ApplyLogicalOverload();
        PendingLogicalMessages.Lease[] batch;
        lock (_pendingPackages) { batch = _pendingPackages.Borrow(OutboundWorkBudget.MaximumMessages); }

        var blocked = new HashSet<(Client Client, NetworkChannel Channel)>();
        var budget = new OutboundWorkBudget(_outboundTime);
        try
        {
            foreach (var lease in batch)
            {
                var package = lease.Package;
                if (!budget.TryConsume(package.Package)) { break; }

                lock (_pendingPackages)
                {
                    if (!lease.Active) { continue; }

                    if (_isStopping || !IsCurrentOutbound(package))
                    {
                        _pendingPackages.Cancel(lease);
                        continue;
                    }
                }

                var transport = PackageTransportPolicy.Get(package.Package);
                var fanout = package.Fanout ??
                             new NetworkMessageFanout(SendStatistics, package.Package.GetType(), transport.Channel);
                var candidates = Runtime.IsAuthority ? Clients.Values :
                    Server is { } server ? new[] { server }.AsEnumerable() : [];
                var recipients = candidates.Where(client =>
                    client.IsConnected && CanSendPackageToClient(package, client, Runtime.IsAuthority)
                ).ToArray();
                lock (_pendingPackages)
                {
                    if (!lease.Active) { continue; }

                    lease.OwnsFanout = false;
                    fanout.Advance(1, recipients.Length);
                }

                var deferred = new List<OutboundPackage>();
                for (var recipientIndex = 0; recipientIndex < recipients.Length; recipientIndex++)
                {
                    var recipient = recipients[recipientIndex];
                    var routed = package with { Audience = PackageAudience.To(recipient), Fanout = fanout };
                    var connection = recipient.Connection!;
                    var key = (recipient, transport.Channel);
                    ConnectionSendResult result;
                    try
                    {
                        result = blocked.Contains(key)
                            ? ConnectionSendResult.Backpressured
                            : connection.TrySendOwned(routed);
                    }
                    catch (Exception exception) when (exception is ObjectDisposedException or IOException)
                    {
                        result = ConnectionSendResult.Closed;
                    }
                    catch
                    {
                        // Earlier accepted sends own their continuation. Only deferred work and the unsent suffix remain ours.
                        fanout.Advance(recipients.Length - recipientIndex + deferred.Count, 0);
                        throw;
                    }

                    if (result == ConnectionSendResult.Backpressured)
                    {
                        blocked.Add(key);
                        deferred.Add(routed);
                    }
                    else if (result == ConnectionSendResult.Closed)
                    {
                        fanout.Advance(1, 0);
                        RemoveClient(
                            recipient,
                            ConnectionMessages.Create(
                                "OutgoingQueueFull",
                                "The outgoing message queue cannot accept another message."
                            )
                        );
                    }
                    else if (connection.IsInProcess)
                    {
                        fanout.DeliveredTo(recipient);
                        fanout.Advance(1, 0);
                    }
                }

                lock (_pendingPackages)
                {
                    _pendingPackages.Release(lease);
                    foreach (var continuation in deferred) { QueueOutboundPackage(continuation, lease.Sequence); }
                }
            }
        }
        finally
        {
            lock (_pendingPackages)
            {
                foreach (var lease in batch)
                {
                    if (!lease.Active) { continue; }

                    if (lease.OwnsFanout) { _pendingPackages.Return(lease); }
                    else { _pendingPackages.Release(lease); }
                }
            }
        }

        FlushNetworkPackages();
    }

    private void FlushNetworkPackages()
    {
        Client[] clients;
        lock (_pendingPackages) { clients = _networkPackages.Keys.ToArray(); }

        if (clients.Length == 0) { return; }

        var now = _outboundTime.GetTimestamp() / (double)_outboundTime.TimestampFrequency;
        var budget = new OutboundWorkBudget(_outboundTime);
        var start = _networkRecipientCursor % clients.Length;
        var inspected = 0;
        var flushedChannels = new HashSet<(Client Client, NetworkChannel Channel)>();
        for (var offset = 0; offset < clients.Length; offset++)
        {
            var position = (start + offset) % clients.Length;
            var client = clients[position];
            var packages = new List<OutboundPackage>();
            var exhausted = false;
            var retainRecipient = false;
            var scanned = 0;
            lock (_pendingPackages)
            {
                if (!_networkPackages.TryGetValue(client, out var pending)) { continue; }

                for (var index = 0; index < pending.Count && packages.Count < 64;)
                {
                    if (inspected > 0 && (inspected >= OutboundWorkBudget.MaximumMessages || budget.HasTimeElapsed))
                    {
                        exhausted = true;
                        break;
                    }

                    inspected++;
                    scanned++;
                    var package = pending[index];
                    if (!IsCurrentOutbound(package) || !client.IsConnected)
                    {
                        package.Fanout?.Advance(1, 0);
                        pending.RemoveAt(index);
                        continue;
                    }

                    var policy = PackageTransportPolicy.Get(package.Package);
                    if (!ShouldFlush(client, policy, now))
                    {
                        index++;
                        continue;
                    }

                    if (!budget.TryConsume(package.Package))
                    {
                        exhausted = true;
                        retainRecipient = packages.Count == 0;
                        break;
                    }

                    packages.Add(package);
                    pending.RemoveAt(index);
                    flushedChannels.Add((client, policy.Channel));
                }

                if (pending.Count == 0) { _networkPackages.Remove(client); }
            }

            if (retainRecipient) { _networkRecipientCursor = position; }
            else if (scanned > 0) { _networkRecipientCursor = (position + 1) % clients.Length; }

            if (packages.Count == 0)
            {
                if (exhausted) { break; }

                continue;
            }

            var deferred = new HashSet<OutboundPackage>(OutboundPackageIdentityComparer.Instance);
            SendPendingPackagesToClient(client, packages, Runtime.IsAuthority, deferred);
            var continuations = new Dictionary<NetworkMessageFanout, (int Consumed, int Deferred)>();
            foreach (var package in packages)
            {
                var fanout = package.Fanout!;
                continuations.TryGetValue(fanout, out var counts);
                continuations[fanout] = (counts.Consumed + 1, counts.Deferred + (deferred.Contains(package) ? 1 : 0));
            }

            // Transfer ownership to the retry work before insertion can close a full queue.
            foreach (var (fanout, counts) in continuations)
            {
                fanout.Advance(counts.Consumed, counts.Deferred);
            }

            for (var index = packages.Count - 1; index >= 0; index--)
            {
                var package = packages[index];
                if (deferred.Contains(package) && !QueueNetworkPackage(client, package, prepend: true))
                {
                    package.Fanout!.Advance(1, 0);
                }
            }

            if (exhausted) { break; }
        }

        lock (_pendingPackages)
        {
            foreach (var channel in flushedChannels)
            {
                if (!Runtime.IsClosed && channel.Client.IsConnected) { _lastPackageFlushTimes[channel] = now; }
            }
        }
    }

    private bool ShouldFlush(Client client, PackageTransport transport, double now)
    {
        if (!_lastPackageFlushTimes.TryGetValue((client, transport.Channel), out var lastFlushTime))
        {
            return true;
        }

        return now - lastFlushTime >= transport.FlushInterval;
    }

    private void SendPendingPackagesToClient(
        Client client,
        List<OutboundPackage> packages,
        bool checkClientState,
        HashSet<OutboundPackage> deferredMessages
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
                deferredMessages.Add(outboundPackage with { Audience = PackageAudience.To(client) });
            }
        }
    }

    private List<OutboundPackage> SendPackageBatches(
        Client client,
        List<OutboundPackage> packages,
        PackageTransport transport
    )
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(packages.Count, ProtocolPacketCodec.MaximumMessagesPerPacket);

        var peer = client.Peer!;
        if (packages.Any(outboundPackage => IsTerrainChunkPackage(outboundPackage.Package)))
        {
            foreach (var outboundPackage in packages)
            {
                var measurements = new List<(Type PackageType, int PayloadBytes)>();
                var chunkWriter = CreatePackageWriter([outboundPackage.Package], transport, out _, measurements);
                if (!IsCurrentOutbound(outboundPackage) || client.Connection?.IsConnected != true) { continue; }

                peer.Send(chunkWriter.Data.AsSpan(0, chunkWriter.Length), transport.ChannelNumber, transport.Delivery);
                SendStatistics.Record(transport.Channel, chunkWriter.Length, measurements);
                outboundPackage.Fanout!.DeliveredTo(client);
            }

            return [];
        }

        var maxPacketSize = peer.GetMaxPayloadSize(transport.Delivery);
        var remainingPackages = new List<OutboundPackage>(packages.Count);
        for (var packageIndex = 0; packageIndex < packages.Count; packageIndex++)
        {
            var outboundPackage = packages[packageIndex];
            if (outboundPackage.Package is PickablePositionsPackage pickables)
            {
                CreatePackageWriter([pickables], transport, out var pickableSize);
                if (pickableSize > maxPacketSize)
                {
                    var result = SendPickableFragments(
                        outboundPackage,
                        client,
                        transport,
                        maxPacketSize,
                        data => client.Connection?.IsConnected == true
                            ? peer.TrySend(data, transport.ChannelNumber, transport.Delivery)
                            : NetworkSendResult.Closed
                    );
                    if (result == NetworkSendResult.Backpressure)
                    {
                        remainingPackages.AddRange(packages.Skip(packageIndex));
                        return remainingPackages;
                    }

                    if (result != NetworkSendResult.Accepted) { return []; }

                    continue;
                }
            }

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
                        if (!IsCurrentOutbound(outboundPackage) || client.Connection?.IsConnected != true) { continue; }

                        peer.Send(
                            chunkWriter.Data.AsSpan(0, chunkWriter.Length),
                            transport.ChannelNumber,
                            transport.Delivery
                        );
                        SendStatistics.Record(transport.Channel, chunkWriter.Length, measurements);
                    }

                    if (chunks.Count > 0 &&
                        IsCurrentOutbound(outboundPackage) &&
                        client.Connection?.IsConnected == true)
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
            if (packages.Any(package => !IsCurrentOutbound(package)) || client.Connection?.IsConnected != true)
            {
                return [];
            }

            var result = peer.TrySend(
                writer.Data.AsSpan(0, writer.Length),
                transport.ChannelNumber,
                transport.Delivery
            );
            if (result == NetworkSendResult.Backpressure) { return remainingPackages; }

            if (result == NetworkSendResult.TooLarge)
            {
                if (remainingPackages.Count == 1)
                {
                    RemoveClient(
                        client,
                        ConnectionMessages.Create(
                            "TransportLimitExceeded",
                            "The outgoing message exceeds the transport send limit."
                        )
                    );
                    return [];
                }

                var middle = remainingPackages.Count / 2;
                var right = remainingPackages.GetRange(middle, remainingPackages.Count - middle);
                var unsent = SendPackageBatches(client, remainingPackages.GetRange(0, middle), transport);
                if (unsent.Count > 0)
                {
                    unsent.AddRange(right);
                    return unsent;
                }

                return client.IsConnected && !_clientsToRemove.ContainsKey(client)
                    ? SendPackageBatches(client, right, transport)
                    : [];
            }

            if (result == NetworkSendResult.Closed) { return []; }

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
        if (selectedPackages.Any(package => !IsCurrentOutbound(package)) || client.Connection?.IsConnected != true)
        {
            return [];
        }

        peer.Send(writer.Data.AsSpan(0, writer.Length), transport.ChannelNumber, transport.Delivery);
        SendStatistics.Record(transport.Channel, writer.Length, measurements);
        foreach (var outboundPackage in selectedPackages)
        {
            outboundPackage.Fanout!.DeliveredTo(client);
        }

        return deferredPackages;
    }

    internal List<PickablePositionsPackage> SplitPickablePackage(
        PickablePositionsPackage package,
        PackageTransport transport,
        int maxPacketSize
    )
    {
        var chunks = new List<PickablePositionsPackage>();
        var items = new List<PickablePosition>();
        foreach (var item in package.Positions)
        {
            items.Add(item);
            CreatePackageWriter([package with { Positions = items }], transport, out var size);
            if (size <= maxPacketSize) { continue; }

            items.RemoveAt(items.Count - 1);
            if (items.Count > 0) { chunks.Add(package with { Positions = items.ToArray() }); }

            items.Clear();
            items.Add(item);
            CreatePackageWriter([package with { Positions = items }], transport, out size);
            if (size > maxPacketSize)
            {
                throw new InvalidOperationException("A pickable position exceeds the transport payload limit.");
            }
        }

        if (items.Count > 0 || package.Positions.Count == 0)
        {
            var chunk = package with { Positions = items.ToArray() };
            CreatePackageWriter([chunk], transport, out var size);
            if (size > maxPacketSize)
            {
                throw new InvalidOperationException(
                    "The pickable snapshot header exceeds the transport payload limit."
                );
            }

            chunks.Add(chunk);
        }

        return chunks;
    }

    internal NetworkSendResult SendPickableFragments(
        OutboundPackage outboundPackage,
        Client client,
        PackageTransport transport,
        int maxPacketSize,
        Func<byte[], NetworkSendResult> send
    )
    {
        var package = (PickablePositionsPackage)outboundPackage.Package;
        foreach (var chunk in SplitPickablePackage(package, transport, maxPacketSize))
        {
            if (!IsCurrentOutbound(outboundPackage)) { return NetworkSendResult.Closed; }

            var measurements = new List<(Type PackageType, int PayloadBytes)>();
            var writer = CreatePackageWriter([chunk], transport, out _, measurements);
            var result = send(writer.Data.AsSpan(0, writer.Length).ToArray());
            if (result != NetworkSendResult.Accepted) { return result; }

            SendStatistics.Record(transport.Channel, writer.Length, measurements);
        }

        if (!IsCurrentOutbound(outboundPackage)) { return NetworkSendResult.Closed; }

        outboundPackage.Fanout!.DeliveredTo(client);
        return NetworkSendResult.Accepted;
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

        if (packages.Any(package => !IsCurrentOutbound(package)) || client.Connection?.IsConnected != true) { return; }

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
        return NetworkWireFraming.Wrap(data);
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

    private bool CanSendPackageToClient(
        OutboundPackage outboundPackage,
        Client client,
        bool checkClientState
    )
    {
        if (_isStopping ||
            IsLogicalRecipientOverloaded(client) ||
            _clientsToRemove.ContainsKey(client) ||
            !outboundPackage.Audience.Includes(client))
        {
            return false;
        }

        if (!IsCurrentOutbound(outboundPackage)) { return false; }

        var package = outboundPackage.Package;

        if (!checkClientState)
        {
            return true;
        }

        if (package is BootstrapPackage or InitialWorldSnapshotPackage)
        {
            return true;
        }

        return client.ConnectionPhase == ConnectionPhase.Live &&
               client.State >= ProtocolReceivePolicy.MinimumState(package);
    }
}
