using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

using LiteNetLib;

using Rendezvous.Protocol;

namespace Engine.Network;

/// <summary>Owns a transport socket and advances signaling independently of a game window.</summary>
public sealed class NetworkHost : IAsyncDisposable
{
    private readonly ConcurrentDictionary<Guid, NetworkConnectionRequest> _incoming = new();
    private readonly int _maxPendingReliableBytes;
    private readonly TimeSpan _preparationTimeout;
    private readonly byte _channelCount;
    private readonly NetManager _network;
    private readonly IPAddress _localIPv4Address;
    private readonly IPAddress _localIPv6Address;
    private readonly RelayPacketLayer _relayLayer = new();
    private readonly CancellationTokenSource _stop = new();
    private readonly ConcurrentDictionary<Guid, (IPEndPoint Endpoint, TaskCompletionSource<SignalMessage> Completion)> _requests = new();
    private readonly Task _pump;
    private int _disposed;
    private readonly ConcurrentDictionary<NetPeer, NetworkConnection> _connections = new();
    private readonly NetworkNotificationQueue _notifications;
    private Exception? _failure;
    private readonly ConcurrentDictionary<NetPeer, TaskCompletionSource<NetworkConnection>> _connects = new();
    private readonly ConcurrentDictionary<(IPEndPoint Beacon, Guid Session), (Guid Participant, DateTimeOffset ExpiresAt)> _registrations = new();
    private readonly ConcurrentDictionary<Guid, CandidateProbe> _probes = new();
    private readonly ConcurrentDictionary<Guid, NetworkRoomRegistration> _roomRegistrations = new();
    private readonly ConcurrentDictionary<Guid, RelayPreparation> _relayPreparations = new();
    private readonly Lock _relayGate = new();
    private readonly Lock _signalGate = new();
    private readonly ConcurrentDictionary<NetPeer, Guid> _peerRelays = new();
    private readonly ConcurrentDictionary<Guid, IPEndPoint> _installedRelays = new();

    public event Action<NetworkRoomRegistration, Exception>? RoomRegistrationFailed;

    public event Action<NetworkDatagramEndpoint, byte[]>? DatagramReceived;

    public event Action<NetworkConnectionRequest>? ConnectionRequested;

    public event Action<NetworkConnection>? Connected;

    public event Action<NetworkConnection, NetworkMessage>? Received;

    public event Action<NetworkConnection, NetworkDisconnectInfo>? Disconnected;

    public NetworkHost(int port = 0, NetworkHostOptions? options = null)
    {
        options ??= new NetworkHostOptions();
        ArgumentNullException.ThrowIfNull(options.LocalIPv4Address);
        ArgumentNullException.ThrowIfNull(options.LocalIPv6Address);
        if (options.LocalIPv4Address.AddressFamily != AddressFamily.InterNetwork ||
            options.LocalIPv6Address.AddressFamily != AddressFamily.InterNetworkV6)
        {
            throw new ArgumentException("Local binding addresses must match their IPv4 and IPv6 address families.", nameof(options));
        }
        _localIPv4Address = options.LocalIPv4Address;
        _localIPv6Address = options.LocalIPv6Address;
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MaxPendingReliableBytes, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MaxDataNotifications, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MaxNotificationBytes, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MaxControlNotifications, 1);
        _notifications = new NetworkNotificationQueue(options.MaxDataNotifications, options.MaxNotificationBytes,
            options.MaxControlNotifications);
        if (options.DisconnectTimeout <= TimeSpan.Zero || options.DisconnectTimeout > TimeSpan.FromMinutes(2))
        {
            throw new ArgumentOutOfRangeException(nameof(options));
        }
        ArgumentOutOfRangeException.ThrowIfLessThan(options.ChannelCount, (byte)1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(options.ChannelCount, (byte)64);
        if (options.PreparationTimeout <= TimeSpan.Zero || options.PreparationTimeout > TimeSpan.FromMinutes(2))
        {
            throw new ArgumentOutOfRangeException(nameof(options));
        }
        _preparationTimeout = options.PreparationTimeout;
        _channelCount = options.ChannelCount;
        _maxPendingReliableBytes = options.MaxPendingReliableBytes;
        var timeout = options.RequestTimeout;
        if (timeout <= TimeSpan.Zero || timeout > TimeSpan.FromSeconds(30))
        {
            throw new ArgumentOutOfRangeException(nameof(options));
        }
        var listener = new EventBasedNetListener();
        listener.NetworkReceiveUnconnectedEvent += (endpoint, reader, messageType) =>
        {
            try
            {
                if (reader.AvailableBytes == 0 || reader.AvailableBytes > SignalCodec.MaxDatagramBytes)
                {
                    return;
                }
                var payload = reader.GetRemainingBytes();
                if (!SignalCodec.TryDecode(payload, out var message) || message is null)
                {
                    // Reserve the control prefix even when a control message is malformed.
                    if (payload.AsSpan().StartsWith("SCRV"u8))
                    {
                        return;
                    }
                    var source = new NetworkDatagramEndpoint(this, endpoint, messageType == UnconnectedMessageType.Broadcast);
                    _notifications.TryEnqueue(() => DatagramReceived?.Invoke(source, payload), payload.Length);
                    return;
                }

                if (message.Operation == "relay-probe" && _relayLayer.Contains(endpoint))
                {
                    SendSignal(message with { Operation = "relay-ready" }, endpoint);
                }

                lock (_signalGate)
                {
                    if (message.Operation == "candidate" &&
                        _registrations.TryGetValue((endpoint, message.SessionId), out var registration) &&
                        registration.ExpiresAt > DateTimeOffset.UtcNow &&
                        (registration.Participant == Guid.Empty ||
                         registration.Participant == message.ParticipantId &&
                         _requests.TryGetValue(message.RequestId, out var request) && request.Endpoint.Equals(endpoint)))
                    {
                        AddProbe(message);
                    }
                }

                if (_probes.TryGetValue(message.RequestId, out var probe) && probe.Matches(message, endpoint))
                {
                    if (message.Operation == "punch")
                    {
                        SendSignal(message with { Operation = "punch-ack" }, endpoint);
                    }
                    else if (message.Operation == "punch-ack")
                    {
                        probe.ConfirmReachability(endpoint);
                    }
                }

                if (_requests.TryGetValue(message.RequestId, out var pending) && pending.Endpoint.Equals(endpoint))
                {
                    pending.Completion.TrySetResult(message);
                }
            }
            finally
            {
                reader.Recycle();
            }
        };
        listener.ConnectionRequestEvent += request =>
        {
            if (_incoming.Count >= 128 || request.Data.AvailableBytes > 4096)
            {
                request.Reject("request-limit"u8.ToArray());
                return;
            }
            NetworkConnectionRequest incoming;
            lock (_relayGate)
            {
                incoming = new NetworkConnectionRequest(request, timeout, GetConnection, _relayGate);
                _incoming.TryAdd(incoming.Id, incoming);
            }
            EnqueueControl(() =>
            {
                if (!incoming.IsPending)
                {
                    return;
                }
                if (ConnectionRequested is null)
                {
                    incoming.Reject();
                }
                else
                {
                    ConnectionRequested(incoming);
                }
            });
        };
        listener.PeerConnectedEvent += peer =>
        {
            var pathKind = _relayLayer.Contains(peer.EndPoint) ? NetworkPathKind.Relay :
                _probes.Values.Any(item => item.Endpoint.Equals(peer.EndPoint) && item.Completion.Task.IsCompletedSuccessfully)
                    ? NetworkPathKind.HolePunch : NetworkPathKind.Direct;
            foreach (var pair in _probes.Where(item => item.Value.Endpoint.Equals(peer.EndPoint)))
            {
                _probes.TryRemove(pair.Key, out _);
            }
            NetworkConnection connection;
            lock (_relayGate)
            {
                foreach (var pair in _relayPreparations.Where(item => item.Value.Endpoint.Equals(peer.EndPoint)))
                {
                    _relayPreparations.TryRemove(pair.Key, out _);
                }
                connection = GetConnection(peer);
                connection.PathKind = pathKind;
            }
            if (_connects.TryRemove(peer, out var completion))
            {
                completion.TrySetResult(connection);
            }

            EnqueueControl(() => Connected?.Invoke(connection));
        };
        listener.PeerDisconnectedEvent += (peer, disconnectInfo) =>
        {
            var reason = disconnectInfo.Reason switch
            {
                DisconnectReason.ConnectionFailed => NetworkDisconnectReason.Failed,
                DisconnectReason.Timeout => NetworkDisconnectReason.Timeout,
                DisconnectReason.HostUnreachable or DisconnectReason.NetworkUnreachable or DisconnectReason.UnknownHost => NetworkDisconnectReason.Unreachable,
                DisconnectReason.RemoteConnectionClose => NetworkDisconnectReason.RemoteClose,
                DisconnectReason.DisconnectPeerCalled => NetworkDisconnectReason.LocalClose,
                DisconnectReason.ConnectionRejected => NetworkDisconnectReason.Rejected,
                DisconnectReason.InvalidProtocol => NetworkDisconnectReason.ProtocolError,
                DisconnectReason.Reconnect or DisconnectReason.PeerToPeerConnection => NetworkDisconnectReason.Interrupted,
                _ => NetworkDisconnectReason.Unknown
            };
            var data = disconnectInfo.AdditionalData is { AvailableBytes: > 0 } reader
                ? reader.GetRemainingBytes().Take(256).ToArray()
                : Array.Empty<byte>();
            var information = new NetworkDisconnectInfo(reason, data, (int)disconnectInfo.SocketErrorCode);
            lock (_relayGate)
            {
                if (_peerRelays.TryRemove(peer, out var routeId))
                {
                    ReleaseRelay(routeId, peer.EndPoint);
                }
            }
            if (_connects.TryRemove(peer, out var pending))
            {
                pending.TrySetException(new NetworkConnectionException(information));
            }

            if (_connections.TryRemove(peer, out var connection))
            {
                connection.FailPendingDeliveries();
                EnqueueControl(() => Disconnected?.Invoke(connection, information));
            }
        };
        listener.DeliveryEvent += (peer, userData) =>
        {
            if (userData is PendingDelivery delivery && _connections.TryGetValue(peer, out var connection))
            {
                connection.ConfirmDelivery(delivery);
            }
        };
        listener.NetworkReceiveEvent += (peer, reader, delivery) =>
        {
            try
            {
                if (_connections.TryGetValue(peer, out var connection))
                {
                    var payload = reader.GetRemainingBytes();
                    DeliveryMode? mode = delivery switch
                    {
                        DeliveryMethod.ReliableOrdered => DeliveryMode.ReliableOrdered,
                        DeliveryMethod.ReliableUnordered => DeliveryMode.ReliableUnordered,
                        DeliveryMethod.Sequenced => DeliveryMode.Sequenced,
                        DeliveryMethod.Unreliable => DeliveryMode.Unreliable,
                        _ => null
                    };
                    if (mode is null)
                    {
                        peer.Disconnect("unsupported-delivery"u8.ToArray());
                        return;
                    }
                    if (!_notifications.TryEnqueue(() => Received?.Invoke(connection, new NetworkMessage(payload, mode.Value)), payload.Length) &&
                        mode is DeliveryMode.ReliableOrdered or DeliveryMode.ReliableUnordered)
                    {
                        peer.Disconnect("receive-overload"u8.ToArray());
                    }
                }
            }
            finally
            {
                reader.Recycle();
            }
        };
        _network = new NetManager(listener, _relayLayer)
        {
            UnconnectedMessagesEnabled = true,
            ChannelsCount = options.ChannelCount,
            EnableStatistics = true,
            UseSafeMtu = true,
            ReuseAddress = options.ReuseAddress,
            BroadcastReceiveEnabled = options.ReceiveBroadcasts,
            DisconnectTimeout = (int)options.DisconnectTimeout.TotalMilliseconds
        };
        if (!_network.Start(options.LocalIPv4Address, options.LocalIPv6Address, port))
        {
            throw new InvalidOperationException("Unable to bind network host.");
        }

        _pump = Task.Run(PumpAsync);
    }

    private NetworkConnection GetConnection(NetPeer peer)
    {
        lock (_relayGate)
        {
            TrackRelayPeer(peer);
            return _connections.GetOrAdd(peer, _ => new NetworkConnection(peer, _maxPendingReliableBytes, _channelCount)
            {
                PathKind = _relayLayer.Contains(peer.EndPoint) ? NetworkPathKind.Relay : NetworkPathKind.Direct
            });
        }
    }

    private void TrackRelayPeer(NetPeer peer)
    {
        var installed = _installedRelays.FirstOrDefault(item => item.Value.Equals(peer.EndPoint));
        if (installed.Key != Guid.Empty)
        {
            _peerRelays.TryAdd(peer, installed.Key);
        }
    }

    internal void RegisterRelayRoute(IPEndPoint endpoint, RelayRouteCredentials credentials)
    {
        _relayLayer.Add(endpoint, credentials.RouteId, credentials.Side, credentials.Key);
        _installedRelays.TryAdd(credentials.RouteId, endpoint);
    }

    public int LocalPort => _network.LocalPort;

    public Exception? Failure => Volatile.Read(ref _failure);

    public Task Completion => _pump;

    public int PendingNotificationCount => _notifications.Count;

    public int PendingPathCount => _probes.Count;

    public NetworkHostResources Resources => new(_connections.Count, _connects.Count, _requests.Count,
        _probes.Count, _registrations.Count, _roomRegistrations.Count, _relayPreparations.Count,
        _installedRelays.Count, _notifications.Count);

    public async Task<NetworkRoomRegistration> RegisterRoomAsync(RendezvousClient signaling, IReadOnlyList<IPEndPoint> beacons,
        int maxParticipants, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        ArgumentNullException.ThrowIfNull(beacons);
        if (beacons.Count is < 1 or > 2 || beacons.Select(item => item.AddressFamily).Distinct().Count() != beacons.Count)
        {
            throw new ArgumentException("Provide one beacon endpoint per address family, at most two.", nameof(beacons));
        }
        var endpoints = beacons.Select(item => new IPEndPoint(item.Address, item.Port)).ToArray();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _stop.Token);
        deadline.CancelAfter(TimeSpan.FromSeconds(8));
        var registration = await signaling.CreateAsync(maxParticipants, deadline.Token).ConfigureAwait(false);
        try
        {
            var ticket = await signaling.IssueTicketAsync(registration.SessionId, Guid.Empty,
                registration.HostCredential, deadline.Token).ConfigureAwait(false);
            await RegisterRoomEndpointsAsync(endpoints, registration.SessionId, ticket.Token, deadline.Token).ConfigureAwait(false);
            deadline.Token.ThrowIfCancellationRequested();
            var invitation = new NetworkRoomRegistration(registration.SessionId, registration.JoinCode, registration.ExpiresAt, _stop.Token,
                async token =>
                {
                    var renewal = await signaling.RenewAsync(registration.SessionId, registration.HostCredential, token).ConfigureAwait(false);
                    var renewedTicket = await signaling.IssueTicketAsync(registration.SessionId, Guid.Empty,
                        registration.HostCredential, token).ConfigureAwait(false);
                    await RegisterRoomEndpointsAsync(endpoints, registration.SessionId, renewedTicket.Token, token).ConfigureAwait(false);
                    return renewal.ExpiresAt;
                },
                async token =>
                {
                    try
                    {
                        await signaling.CloseAsync(registration.SessionId, registration.HostCredential, token).ConfigureAwait(false);
                    }
                    finally
                    {
                        _roomRegistrations.TryRemove(registration.SessionId, out _);
                        ReleaseRoomPreparations(registration.SessionId);
                    }
                },
                (item, exception) => EnqueueControl(() => RoomRegistrationFailed?.Invoke(item, exception)),
                (publication, token) => signaling.UpdateRoomAsync(registration.SessionId, registration.HostCredential, publication, token),
                async token =>
                {
                    var offers = await signaling.GetRelayOffersAsync(registration.SessionId, registration.HostCredential, token).ConfigureAwait(false);
                    var deferred = false;
                    foreach (var offer in offers)
                    {
                        if (offer.SessionId != registration.SessionId || offer.Credentials is null || offer.Credentials.Side != 0)
                        {
                            throw new RendezvousException("relay/host-offers", RendezvousFailureKind.InvalidResponse, "invalid_host_relay_offer");
                        }
                        if (offer.ExpiresAt <= DateTimeOffset.UtcNow)
                        {
                            continue;
                        }
                        if (TryInstallRelay(offer) is null)
                        {
                            deferred = true;
                            continue;
                        }
                        await signaling.AcknowledgeRelayAsync(offer, registration.HostCredential, token).ConfigureAwait(false);
                    }
                    if (deferred)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(1), token).ConfigureAwait(false);
                    }
                });
            _roomRegistrations.TryAdd(registration.SessionId, invitation);
            return invitation;
        }
        catch
        {
            ReleaseRoomPreparations(registration.SessionId);
            using var closeDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            try
            {
                await signaling.CloseAsync(registration.SessionId, registration.HostCredential, closeDeadline.Token).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is RendezvousException or OperationCanceledException)
            {
                // If the control endpoint is unavailable, its bounded registration lease handles cleanup.
            }
            throw;
        }
    }

    public async Task<ConnectionPreparation> PrepareConnectionAsync(NetworkTarget target, HttpClient http,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _stop.Token);
        deadline.CancelAfter(_preparationTimeout);
        try
        {
            if (target is AddressNetworkTarget address)
            {
                var direct = await PrepareAddressAsync(address.Host, address.Port, deadline.Token).ConfigureAwait(false);
                direct.Target = address;
                return direct;
            }
            var (control, policy) = target switch
            {
                InvitationCodeNetworkTarget code => (code.ControlAddress, code.RelayPolicy),
                InvitationSessionNetworkTarget session => (session.ControlAddress, session.RelayPolicy),
                _ => throw new ArgumentException("Unknown network target.", nameof(target))
            };
            var signaling = new RendezvousClient(http, control);
            var beacons = await ResolveBeaconsAsync(signaling, control, deadline.Token).ConfigureAwait(false);
            var requestId = Guid.NewGuid();
            var participant = target is InvitationCodeNetworkTarget invitation
                ? await signaling.JoinAsync(invitation.JoinCode, requestId, deadline.Token).ConfigureAwait(false)
                : await signaling.JoinSessionAsync(((InvitationSessionNetworkTarget)target).SessionId, requestId, deadline.Token).ConfigureAwait(false);
            var prepared = await PrepareRegisteredPathAsync(signaling, beacons, participant, policy, deadline.Token).ConfigureAwait(false);
            prepared.Target = new InvitationSessionNetworkTarget(control, participant.SessionId, policy);
            return prepared;
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested && !_stop.IsCancellationRequested)
        {
            if (target is AddressNetworkTarget)
            {
                throw new TimeoutException("Connection preparation timed out.", exception);
            }
            throw new RendezvousException("prepare", RendezvousFailureKind.Timeout, "preparation_timeout", innerException: exception);
        }
    }

    public async Task<NetworkRoomRegistration> RegisterRoomAsync(Uri controlAddress, HttpClient http,
        int maxParticipants, CancellationToken cancellationToken)
    {
        var address = RendezvousAddress.Normalize(controlAddress);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _stop.Token);
        deadline.CancelAfter(_preparationTimeout);
        var signaling = new RendezvousClient(http, address);
        var beacons = await ResolveBeaconsAsync(signaling, address, deadline.Token).ConfigureAwait(false);
        return await RegisterRoomAsync(signaling, beacons, maxParticipants, deadline.Token).ConfigureAwait(false);
    }

    private static async Task<IPEndPoint[]> ResolveBeaconsAsync(RendezvousClient signaling, Uri controlAddress,
        CancellationToken cancellationToken)
    {
        var descriptor = await signaling.GetDescriptorAsync(cancellationToken).ConfigureAwait(false);
        if (descriptor.UdpPort is < 1 or > 65535 || descriptor.UdpHost is not null && string.IsNullOrWhiteSpace(descriptor.UdpHost))
        {
            throw new RendezvousException("transport", RendezvousFailureKind.InvalidResponse, "invalid_udp_descriptor");
        }
        var addresses = await Dns.GetHostAddressesAsync(descriptor.UdpHost ?? controlAddress.DnsSafeHost, cancellationToken).ConfigureAwait(false);
        var endpoints = addresses
            .Where(item => item.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6 &&
                           !item.Equals(IPAddress.Any) && !item.Equals(IPAddress.IPv6Any))
            .GroupBy(item => item.AddressFamily)
            .Select(group => new IPEndPoint(group.First(), descriptor.UdpPort))
            .ToArray();
        if (endpoints.Length == 0)
        {
            throw new RendezvousException("transport", RendezvousFailureKind.Unreachable, "udp_host_unresolved");
        }
        return endpoints;
    }

    public async Task<ConnectionPreparation> PrepareAddressAsync(string address, int port,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        ArgumentOutOfRangeException.ThrowIfLessThan(port, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(port, 65535);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _stop.Token);
        var addresses = await Dns.GetHostAddressesAsync(address, deadline.Token).ConfigureAwait(false);
        var endpointAddress = addresses.FirstOrDefault(item => item.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            ?? addresses.FirstOrDefault() ?? throw new IOException("Address did not resolve.");
        deadline.Token.ThrowIfCancellationRequested();
        return new ConnectionPreparation(this, new IPEndPoint(endpointAddress, port), NetworkPathKind.Direct,
            DateTimeOffset.UtcNow + TimeSpan.FromMinutes(2), null, TimeProvider.System);
    }

    public async Task<ConnectionPreparation> PrepareInvitationAsync(RendezvousClient signaling, IReadOnlyList<IPEndPoint> beacons,
        string joinCode, RelayPolicy relayPolicy, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (!Enum.IsDefined(relayPolicy))
        {
            throw new ArgumentOutOfRangeException(nameof(relayPolicy));
        }
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _stop.Token);
        var participant = await signaling.JoinAsync(joinCode, Guid.NewGuid(), deadline.Token).ConfigureAwait(false);
        return await PrepareRegisteredPathAsync(signaling, beacons, participant, relayPolicy, deadline.Token).ConfigureAwait(false);
    }

    private async Task<ConnectionPreparation> PrepareRegisteredPathAsync(RendezvousClient signaling, IReadOnlyList<IPEndPoint> beacons,
        ParticipantRegistration participant, RelayPolicy relayPolicy, CancellationToken cancellationToken)
    {
        try
        {
            var prepared = await PrepareParticipantPathAsync(signaling, beacons, participant, relayPolicy, cancellationToken).ConfigureAwait(false);
            prepared.SetRegistrationRelease(async token =>
            {
                try
                {
                    await signaling.LeaveAsync(participant, token).ConfigureAwait(false);
                }
                finally
                {
                    foreach (var beacon in beacons)
                    {
                        _registrations.TryRemove((beacon, participant.SessionId), out _);
                    }
                }
            });
            return prepared;
        }
        catch
        {
            foreach (var beacon in beacons)
            {
                _registrations.TryRemove((beacon, participant.SessionId), out _);
            }
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            try
            {
                await signaling.LeaveAsync(participant, deadline.Token).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is RendezvousException or OperationCanceledException)
            {
                // Unreachable control service cannot acknowledge release; the bounded registration expires.
            }
            throw;
        }
    }

    private async Task<ConnectionPreparation> PrepareParticipantPathAsync(RendezvousClient signaling, IReadOnlyList<IPEndPoint> beacons,
        ParticipantRegistration participant, RelayPolicy relayPolicy, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _stop.Token);
        if (relayPolicy != RelayPolicy.Required)
        {
            try
            {
                var ticket = await signaling.IssueTicketAsync(participant.SessionId, participant.ParticipantId,
                    participant.Credential, deadline.Token).ConfigureAwait(false);
                return await PrepareCandidateRaceAsync(beacons, participant, ticket.Token, deadline.Token).ConfigureAwait(false);
            }
            catch (Exception exception) when (relayPolicy == RelayPolicy.Allowed && !deadline.IsCancellationRequested &&
                                              (exception is TimeoutException or OperationCanceledException ||
                                               exception is RendezvousException { Kind: RendezvousFailureKind.UdpUnavailable or RendezvousFailureKind.HolePunchUnavailable }))
            {
                // The direct candidate budget expired; keep the participant and try its authorized relay path.
            }
        }
        var allocation = await signaling.AllocateRelayAsync(participant.SessionId, participant.ParticipantId,
            Guid.NewGuid(), participant.Credential, deadline.Token).ConfigureAwait(false);
        if (allocation.SessionId != participant.SessionId || allocation.ParticipantId != participant.ParticipantId ||
            allocation.Credentials is null || allocation.Credentials.Side != 1)
        {
            throw new RendezvousException("relay/allocate", RendezvousFailureKind.InvalidResponse, "invalid_client_relay_allocation");
        }
        var relayEndpoint = TryInstallRelay(allocation) ??
                            throw new RendezvousException("relay/install", RendezvousFailureKind.RelayUnavailable, "relay_endpoint_busy");
        try
        {
            var response = await RequestSignalAsync(relayEndpoint,
                new SignalMessage("relay-probe", Guid.NewGuid(), participant.SessionId, participant.ParticipantId), deadline.Token,
                TimeSpan.FromSeconds(12)).ConfigureAwait(false);
            if (response.Operation != "relay-ready")
            {
                throw new RendezvousException("relay/probe", RendezvousFailureKind.InvalidResponse, "invalid_relay_response");
            }
            var prepared = new ConnectionPreparation(this, relayEndpoint, NetworkPathKind.Relay, allocation.ExpiresAt,
                () => ReleaseRelay(allocation.Credentials.RouteId, relayEndpoint), TimeProvider.System);
            prepared.SetLeaseRenewal(async (duration, token) =>
            {
                await signaling.RenewParticipantAsync(participant, token).ConfigureAwait(false);
                var renewed = await signaling.RenewRelayPreparationAsync(participant, allocation.AttemptId, token).ConfigureAwait(false);
                if (renewed.SessionId != allocation.SessionId || renewed.ParticipantId != allocation.ParticipantId ||
                    renewed.AttemptId != allocation.AttemptId || renewed.Endpoint != allocation.Endpoint ||
                    renewed.Credentials is null || renewed.Credentials.RouteId != allocation.Credentials.RouteId || renewed.Credentials.Side != 1 ||
                    !renewed.Credentials.Key.AsSpan().SequenceEqual(allocation.Credentials.Key))
                {
                    throw new RendezvousException("relay/renew", RendezvousFailureKind.InvalidResponse, "relay_path_changed");
                }
                if (TryInstallRelay(renewed) is null)
                {
                    throw new RendezvousException("relay/renew", RendezvousFailureKind.RelayUnavailable, "relay_endpoint_busy");
                }
                return renewed.ExpiresAt;
            });
            return prepared;
        }
        catch
        {
            ReleaseRelay(allocation.Credentials.RouteId, relayEndpoint);
            throw;
        }
    }

    private async Task<ConnectionPreparation> PrepareCandidateRaceAsync(IReadOnlyList<IPEndPoint> beacons,
        ParticipantRegistration participant, string ticket, CancellationToken cancellationToken)
    {
        if (beacons.Count is < 1 or > 2 || beacons.Select(item => item.AddressFamily).Distinct().Count() != beacons.Count)
        {
            throw new ArgumentException("Provide one beacon endpoint per address family, at most two.", nameof(beacons));
        }
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _stop.Token);
        var preparations = beacons.Select(beacon =>
            (Func<CancellationToken, Task<ConnectionPreparation>>)(token => PrepareCandidateAsync(beacon, participant, ticket, token))).ToArray();
        return await CandidatePreparationRace.SelectAsync(preparations,
            index => _registrations.TryRemove((beacons[index], participant.SessionId), out _), lifetime.Token).ConfigureAwait(false);
    }

    private async Task<ConnectionPreparation> PrepareCandidateAsync(IPEndPoint beacon,
        ParticipantRegistration participant, string ticket, CancellationToken cancellationToken)
    {
        await RegisterAsync(beacon, participant.SessionId, participant.ParticipantId, ticket, cancellationToken).ConfigureAwait(false);
        var candidate = await RequestSignalAsync(beacon,
            new SignalMessage("exchange", Guid.NewGuid(), participant.SessionId, participant.ParticipantId,
                ticket), cancellationToken).ConfigureAwait(false);
        try
        {
            var endpoint = await ProbeCandidateAsync(candidate, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (!_probes.TryGetValue(candidate.RequestId, out var path))
            {
                throw new TimeoutException("Prepared path expired.");
            }
            var prepared = new ConnectionPreparation(this, endpoint, NetworkPathKind.HolePunch, path.ExpiresAt,
                () => _probes.TryRemove(candidate.RequestId, out _), TimeProvider.System);
            path.OwnerExpiresAt = prepared.ExpiresAt;
            prepared.SetLeaseRenewal((duration, token) =>
            {
                token.ThrowIfCancellationRequested();
                if (!_probes.TryGetValue(candidate.RequestId, out var current) || current.ExpiresAt <= DateTimeOffset.UtcNow)
                {
                    throw new TimeoutException("Prepared candidate path expired.");
                }
                current.OwnerExpiresAt = DateTimeOffset.UtcNow + duration;
                return Task.FromResult(current.OwnerExpiresAt!.Value);
            });
            return prepared;
        }
        catch
        {
            _probes.TryRemove(candidate.RequestId, out _);
            throw;
        }
    }

    internal IPEndPoint? TryInstallRelay(RelayAllocation allocation)
    {
        if (!IPEndPoint.TryParse(allocation.Endpoint, out var endpoint) || endpoint.Port == 0 ||
            endpoint.Address.Equals(IPAddress.Any) || endpoint.Address.Equals(IPAddress.IPv6Any) ||
            allocation.ExpiresAt <= DateTimeOffset.UtcNow || allocation.Credentials is null ||
            allocation.Credentials.RouteId == Guid.Empty || allocation.Credentials.Side > 1 ||
            allocation.Credentials.Key is not { Length: 32 })
        {
            throw new RendezvousException("relay/install", RendezvousFailureKind.InvalidResponse, "invalid_relay_allocation");
        }
        lock (_relayGate)
        {
            if (!_installedRelays.ContainsKey(allocation.Credentials.RouteId) &&
                (_connections.Values.Any(connection => connection.IsConnected && connection.TransportEndpoint.Equals(endpoint)) ||
                 _connects.Keys.Any(peer => peer.EndPoint.Equals(endpoint)) ||
                 _incoming.Values.Any(request => request.IsPending && request.TransportEndpoint.Equals(endpoint))))
            {
                return null;
            }
            foreach (var old in _installedRelays.Where(item => item.Value.Equals(endpoint) && item.Key != allocation.Credentials.RouteId))
            {
                ReleaseRelay(old.Key, old.Value);
            }
            if (!_installedRelays.ContainsKey(allocation.Credentials.RouteId))
            {
                _relayLayer.Add(endpoint, allocation.Credentials.RouteId, allocation.Credentials.Side, allocation.Credentials.Key);
                _installedRelays.TryAdd(allocation.Credentials.RouteId, endpoint);
                _relayPreparations.TryAdd(allocation.Credentials.RouteId, new RelayPreparation(endpoint, allocation.ExpiresAt, allocation.SessionId));
            }
            else if (!_relayLayer.Matches(endpoint, allocation.Credentials.RouteId, allocation.Credentials.Side, allocation.Credentials.Key))
            {
                throw new RendezvousException("relay/renew", RendezvousFailureKind.InvalidResponse, "relay_credentials_changed");
            }
            if (_relayPreparations.TryGetValue(allocation.Credentials.RouteId, out var prepared))
            {
                prepared.ExpiresAt = allocation.ExpiresAt;
            }
            SendSignal(new SignalMessage("relay-enroll", Guid.NewGuid()), endpoint);
            return endpoint;
        }
    }

    internal void ReleaseRoomPreparations(Guid sessionId)
    {
        lock (_signalGate)
        {
            foreach (var pair in _registrations.Where(item => item.Key.Item2 == sessionId).ToArray())
            {
                _registrations.TryRemove(pair);
            }
            foreach (var pair in _probes.Where(item => item.Value.Candidate.SessionId == sessionId).ToArray())
            {
                if (_probes.TryRemove(pair.Key, out var probe))
                {
                    probe.Completion.TrySetCanceled();
                }
            }
        }
        lock (_relayGate)
        {
            foreach (var pair in _relayPreparations.Where(item => item.Value.SessionId == sessionId).ToArray())
            {
                if (!_connections.Values.Any(connection => connection.IsConnected &&
                        connection.TransportEndpoint.Equals(pair.Value.Endpoint)))
                {
                    ReleaseRelay(pair.Key, pair.Value.Endpoint);
                }
            }
        }
    }

    private void ReleaseRelay(Guid routeId, IPEndPoint endpoint)
    {
        lock (_relayGate)
        {
            _relayPreparations.TryRemove(routeId, out _);
            if (_installedRelays.TryRemove(routeId, out _))
            {
                for (var attempt = 0; attempt < 3; attempt++)
                {
                    SendSignal(new SignalMessage("relay-close", Guid.NewGuid()), endpoint);
                }
                _relayLayer.Remove(endpoint);
            }
        }
    }

    public async Task<SignalMessage> RequestSignalAsync(IPEndPoint beacon, SignalMessage request,
        CancellationToken cancellationToken, TimeSpan? requestTimeout = null)
    {
        var timeout = requestTimeout ?? TimeSpan.FromSeconds(3);
        if (timeout <= TimeSpan.Zero || timeout > TimeSpan.FromSeconds(15))
        {
            throw new ArgumentOutOfRangeException(nameof(requestTimeout));
        }
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        var completion = new TaskCompletionSource<SignalMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_requests.TryAdd(request.RequestId, (beacon, completion)))
        {
            throw new InvalidOperationException("Duplicate signaling request ID.");
        }

        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _stop.Token);
            deadline.CancelAfter(timeout);
            var payload = SignalCodec.Encode(request);
            while (!completion.Task.IsCompleted)
            {
                _network.SendUnconnectedMessage(payload, beacon);
                var delay = Task.Delay(250, deadline.Token);
                await Task.WhenAny(completion.Task, delay).ConfigureAwait(false);
                deadline.Token.ThrowIfCancellationRequested();
            }

            var response = await completion.Task.ConfigureAwait(false);
            if (response.Error is not null)
            {
                var kind = response.Error switch
                {
                    "registration_unauthorized" or "invitation_unauthorized" => RendezvousFailureKind.Unauthorized,
                    "host_not_registered" => RendezvousFailureKind.HolePunchUnavailable,
                    "invalid_capacity" or "unknown_operation" => RendezvousFailureKind.InvalidRequest,
                    _ => RendezvousFailureKind.Rejected
                };
                throw new RendezvousException(request.Operation, kind, response.Error);
            }

            return response;
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested && !_stop.IsCancellationRequested)
        {
            var kind = request.Operation == "relay-probe" ? RendezvousFailureKind.RelayUnavailable : RendezvousFailureKind.UdpUnavailable;
            throw new RendezvousException(request.Operation, kind, "udp_request_timeout", innerException: exception);
        }
        finally
        {
            lock (_signalGate)
            {
                _requests.TryRemove(request.RequestId, out _);
                if (!completion.Task.IsCompletedSuccessfully || cancellationToken.IsCancellationRequested || _stop.IsCancellationRequested)
                {
                    _probes.TryRemove(request.RequestId, out _);
                }
            }
        }
    }

    private async Task RegisterRoomEndpointsAsync(IReadOnlyList<IPEndPoint> endpoints, Guid sessionId,
        string ticket, CancellationToken cancellationToken)
    {
        async Task<Exception?> RegisterEndpointAsync(IPEndPoint endpoint)
        {
            try
            {
                await RegisterAsync(endpoint, sessionId, Guid.Empty, ticket, cancellationToken).ConfigureAwait(false);
                return null;
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested &&
                                              exception is RendezvousException { Kind: RendezvousFailureKind.UdpUnavailable or RendezvousFailureKind.Unreachable } or SocketException)
            {
                return exception;
            }
        }
        var failures = await Task.WhenAll(endpoints.Select(RegisterEndpointAsync)).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (failures.All(item => item is not null))
        {
            throw failures[0]!;
        }
    }

    private string[] GetLocalCandidateEndpoints()
    {
        var addresses = NetworkInterface.GetAllNetworkInterfaces()
            .Where(item => item.OperationalStatus == OperationalStatus.Up)
            .SelectMany(item => item.GetIPProperties().UnicastAddresses)
            .Select(item => item.Address)
            .Where(address => LocalCandidateEndpoints.IsCandidateAddress(address) &&
                (address.AddressFamily == AddressFamily.InterNetwork
                    ? _localIPv4Address.Equals(IPAddress.Any) || _localIPv4Address.Equals(address)
                    : _localIPv6Address.Equals(IPAddress.IPv6Any) || _localIPv6Address.Equals(address)))
            .Concat(new[] { _localIPv4Address, _localIPv6Address }.Where(LocalCandidateEndpoints.IsCandidateAddress));
        return addresses.Distinct()
            .OrderByDescending(LocalCandidateEndpoints.IsGlobalIPv6Address)
            .ThenBy(address => address.ToString(), StringComparer.Ordinal)
            .Take(LocalCandidateEndpoints.MaxCount)
            .Select(address => new IPEndPoint(address, LocalPort).ToString())
            .ToArray();
    }

    public async Task RegisterAsync(IPEndPoint beacon, Guid sessionId, Guid participantId, string ticket,
        CancellationToken cancellationToken)
    {
        var response = await RequestSignalAsync(beacon,
            new SignalMessage("register", Guid.NewGuid(), sessionId, participantId, ticket,
                LocalEndpoints: GetLocalCandidateEndpoints()), cancellationToken).ConfigureAwait(false);
        if (response.Operation != "challenge" || response.Challenge is null)
        {
            throw new RendezvousException("register", RendezvousFailureKind.InvalidResponse, "invalid_registration_challenge");
        }

        var confirmed = await RequestSignalAsync(beacon,
            new SignalMessage("confirm", Guid.NewGuid(), sessionId, participantId, ticket,
                Challenge: response.Challenge), cancellationToken).ConfigureAwait(false);
        if (confirmed.Operation != "registered")
        {
            throw new RendezvousException("confirm", RendezvousFailureKind.InvalidResponse, "invalid_registration_confirmation");
        }
        _registrations[(beacon, sessionId)] = (participantId, DateTimeOffset.UtcNow + TimeSpan.FromMinutes(1));
    }

    public async Task<IPEndPoint> ProbeCandidateAsync(SignalMessage candidate, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (!_probes.TryGetValue(candidate.RequestId, out var probe))
        {
            throw new RendezvousException("candidate", RendezvousFailureKind.InvalidResponse, "unregistered_candidate");
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _stop.Token);
        try
        {
            return await probe.Completion.Task.WaitAsync(TimeSpan.FromSeconds(5), deadline.Token).ConfigureAwait(false);
        }
        catch (TimeoutException exception)
        {
            _probes.TryRemove(candidate.RequestId, out _);
            throw new RendezvousException("candidate", RendezvousFailureKind.HolePunchUnavailable, "candidate_timeout", innerException: exception);
        }
        catch
        {
            _probes.TryRemove(candidate.RequestId, out _);
            throw;
        }
    }

    private void AddProbe(SignalMessage candidate)
    {
        if (candidate.ParticipantId == Guid.Empty || candidate.Challenge is not { Length: 64 } ||
            candidate.Endpoint is null || !IPEndPoint.TryParse(candidate.Endpoint, out var endpoint) ||
            !LocalCandidateEndpoints.TryParse(candidate.LocalEndpoints, null, out var localEndpoints) ||
            _probes.Count >= 128)
        {
            return;
        }

        _probes.TryAdd(candidate.RequestId, new CandidateProbe(candidate, new[] { endpoint }.Concat(localEndpoints).Distinct().ToArray()));
    }

    public void SendDatagram(IPEndPoint endpoint, ReadOnlySpan<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (payload.IsEmpty || payload.Length > 1024 || payload.StartsWith("SCRV"u8))
        {
            throw new ArgumentException("Application datagrams must be 1–1024 bytes without the control prefix.", nameof(payload));
        }
        if (!_network.SendUnconnectedMessage(payload.ToArray(), endpoint))
        {
            throw new IOException("Datagram could not be sent.");
        }
    }

    public void BroadcastDatagram(ReadOnlySpan<byte> payload, int port)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        ArgumentOutOfRangeException.ThrowIfLessThan(port, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(port, 65535);
        if (payload.IsEmpty || payload.Length > 1024 || payload.StartsWith("SCRV"u8))
        {
            throw new ArgumentException("Invalid application broadcast payload.", nameof(payload));
        }
        _network.SendBroadcast(payload.ToArray(), port);
    }

    private void SendSignal(SignalMessage message, IPEndPoint endpoint) =>
        _network.SendUnconnectedMessage(SignalCodec.Encode(message), endpoint);

    public async Task<NetworkConnection> ConnectAsync(IPEndPoint endpoint, ReadOnlyMemory<byte> handshake, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        cancellationToken.ThrowIfCancellationRequested();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _stop.Token);
        var writer = new LiteNetLib.Utils.NetDataWriter();
        writer.Put(handshake.ToArray());
        NetPeer peer;
        var completion = new TaskCompletionSource<NetworkConnection>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_relayGate)
        {
            peer = _network.Connect(endpoint, writer);
            if (!_connects.TryAdd(peer, completion))
            {
                throw new InvalidOperationException("Connection attempt already in progress.");
            }
            TrackRelayPeer(peer);
        }

        try
        {
            if (_connections.TryGetValue(peer, out var existing))
            {
                return existing;
            }

            return await completion.Task.WaitAsync(TimeSpan.FromSeconds(5), deadline.Token).ConfigureAwait(false);
        }
        catch
        {
            peer.Disconnect();
            throw;
        }
        finally
        {
            _connects.TryRemove(peer, out _);
        }
    }

    public void DispatchEvents(int maxEvents = 128)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxEvents, 1);
        for (var index = 0; index < maxEvents && _notifications.TryDequeue(out var notification); index++)
        {
            notification!();
        }
    }

    private void EnqueueControl(Action notification)
    {
        if (!_notifications.TryEnqueue(notification, control: true))
        {
            Interlocked.CompareExchange(ref _failure,
                new IOException("Network lifecycle notification capacity exceeded."), null);
            _stop.Cancel();
        }
    }

    private async Task PumpAsync()
    {
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                _network.PollEvents();
                foreach (var pair in _registrations)
                {
                    if (pair.Value.ExpiresAt <= DateTimeOffset.UtcNow)
                    {
                        _registrations.TryRemove(pair);
                    }
                }
                foreach (var pair in _incoming)
                {
                    pair.Value.Expire();
                    if (!pair.Value.IsPending)
                    {
                        _incoming.TryRemove(pair.Key, out _);
                    }
                }
                foreach (var pair in _relayPreparations)
                {
                    if (pair.Value.ExpiresAt <= DateTimeOffset.UtcNow)
                    {
                        ReleaseRelay(pair.Key, pair.Value.Endpoint);
                    }
                    else if (pair.Value.NextSendAt <= DateTimeOffset.UtcNow)
                    {
                        SendSignal(new SignalMessage("relay-enroll", Guid.NewGuid()), pair.Value.Endpoint);
                        pair.Value.NextSendAt = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(2);
                    }
                }
                foreach (var pair in _probes)
                {
                    var probe = pair.Value;
                    if (probe.ExpiresAt <= DateTimeOffset.UtcNow || probe.OwnerExpiresAt <= DateTimeOffset.UtcNow)
                    {
                        if (_probes.TryRemove(pair.Key, out _))
                        {
                            probe.Completion.TrySetException(new TimeoutException("Candidate probe expired."));
                        }
                    }
                    else if (probe.NextSendAt <= DateTimeOffset.UtcNow)
                    {
                        foreach (var endpoint in probe.SendEndpoints)
                        {
                            _network.SendUnconnectedMessage(SignalCodec.Encode(probe.Candidate with
                            {
                                Operation = "punch",
                                Endpoint = null,
                                LocalEndpoints = null
                            }), endpoint);
                        }
                        probe.NextSendAt = DateTimeOffset.UtcNow + (probe.Completion.Task.IsCompletedSuccessfully
                            ? TimeSpan.FromSeconds(2)
                            : TimeSpan.FromMilliseconds(250));
                    }
                }
                await Task.Delay(5, _stop.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            Volatile.Write(ref _failure, exception);
            throw;
        }
        finally
        {
            foreach (var request in _incoming.Values)
            {
                request.Reject("host-stopped"u8);
            }
            _incoming.Clear();
            foreach (var connection in _connections.Values)
            {
                connection.Disconnect();
            }
            foreach (var pair in _installedRelays)
            {
                ReleaseRelay(pair.Key, pair.Value);
            }
            _network.Stop();
            foreach (var pending in _connects.Values)
            {
                pending.TrySetException(new IOException("Network host stopped."));
            }
            _connects.Clear();

            foreach (var pending in _requests.Values)
            {
                pending.Completion.TrySetException(new IOException("Network host stopped."));
            }
            _requests.Clear();
            _registrations.Clear();
            _roomRegistrations.Clear();
            _relayPreparations.Clear();

            foreach (var connection in _connections.Values)
            {
                connection.FailPendingDeliveries();
            }
            _connections.Clear();
            _peerRelays.Clear();
            foreach (var probe in _probes.Values)
            {
                probe.Completion.TrySetException(new IOException("Network host stopped."));
            }
            _probes.Clear();
            _notifications.Clear();
            if (Failure is { } failure)
            {
                throw failure;
            }
        }
    }

    private sealed class RelayPreparation(IPEndPoint endpoint, DateTimeOffset expiresAt, Guid sessionId)
    {
        public IPEndPoint Endpoint { get; } = endpoint;

        public Guid SessionId { get; } = sessionId;

        private long _expiresAtTicks = expiresAt.UtcTicks;

        public DateTimeOffset ExpiresAt
        {
            get => new(Interlocked.Read(ref _expiresAtTicks), TimeSpan.Zero);
            set => Interlocked.Exchange(ref _expiresAtTicks, value.UtcTicks);
        }

        public DateTimeOffset NextSendAt { get; set; }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            await _pump.ConfigureAwait(false);
            return;
        }

        try
        {
            await Task.WhenAll(_roomRegistrations.Values.Select(item => item.DisposeAsync().AsTask())).ConfigureAwait(false);
        }
        finally
        {
            await _stop.CancelAsync().ConfigureAwait(false);
            try
            {
                await _pump.ConfigureAwait(false);
            }
            finally
            {
                _stop.Dispose();
            }
        }
    }
}
