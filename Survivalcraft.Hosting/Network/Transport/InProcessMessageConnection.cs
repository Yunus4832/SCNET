using Game.Terrains.Distribution;

namespace Game.Network.Transport;

/// <summary>One direction of a trusted in-process link; payloads enter the same inbox as decoded network messages.</summary>
public sealed class InProcessMessageConnection : IMessageConnection
{
    private readonly WorldRuntimeContext _source;

    private readonly PackageReceiveContext _destination;

    private readonly Lifetime _lifetime;

    private InProcessMessageConnection(
        WorldRuntimeContext source,
        PackageReceiveContext destination,
        Lifetime lifetime
    )
    {
        _source = source;
        _destination = destination;
        _lifetime = lifetime;
    }

    public bool IsConnected => ReferenceEquals(WorldNetworkBinding.FindNode(_source)?.Runtime, _source) &&
                               ReferenceEquals(
                                   WorldNetworkBinding.FindNode(_destination.Runtime)?.Runtime,
                                   _destination.Runtime
                               ) &&
                               !_source.IsClosed &&
                               !_destination.Runtime.IsClosed &&
                               !_lifetime.IsClosed;

    public bool IsInProcess => true;

    internal string DisconnectReason => _lifetime.DisconnectReason;

    public static (InProcessMessageConnection ToAuthority, InProcessMessageConnection ToClient) CreatePair(
        NetNode authority,
        Client playerSession,
        NetNode client,
        Client authoritySession
    )
    {
        if (!authority.Runtime.IsAuthority || client.Runtime.IsAuthority)
        {
            throw new ArgumentException("An in-process pair requires an authority endpoint and a client endpoint.");
        }

        if (ReferenceEquals(playerSession, authoritySession) ||
            playerSession.Peer is not null ||
            authoritySession.Peer is not null ||
            playerSession.Node is not null && !ReferenceEquals(playerSession.Node, authority) ||
            authoritySession.Node is not null && !ReferenceEquals(authoritySession.Node, client))
        {
            throw new ArgumentException("Each in-process endpoint requires its own local session record.");
        }

        ObjectDisposedException.ThrowIf(authority.Runtime.IsClosed, authority.Runtime);
        ObjectDisposedException.ThrowIf(client.Runtime.IsClosed, client.Runtime);
        if (playerSession.World is not null && !ReferenceEquals(playerSession.World, authority.Runtime) ||
            authoritySession.World is not null && !ReferenceEquals(authoritySession.World, client.Runtime) ||
            client.Server is not null && !ReferenceEquals(client.Server, authoritySession) ||
            authority.Clients.TryGetValue(playerSession.ID, out var occupant) &&
            !ReferenceEquals(occupant, playerSession))
        {
            throw new InvalidOperationException("An in-process link cannot replace an existing world connection.");
        }

        var registered = authority.Clients.ContainsKey(playerSession.ID);
        var previousAuthority = client.Server;
        authority.AddClient(playerSession);
        try { client.Server = authoritySession; }
        catch
        {
            if (!registered) { authority.UnregisterClient(playerSession); }

            client.Server = previousAuthority;
            throw;
        }

        var lifetime = new Lifetime(authority.Runtime.Lifetime, client.Runtime.Lifetime);
        var toAuthority = new InProcessMessageConnection(
            client.Runtime,
            authority.CreateReceiveContext(playerSession),
            lifetime
        );
        var toClient = new InProcessMessageConnection(
            authority.Runtime,
            client.CreateReceiveContext(authoritySession),
            lifetime
        );
        playerSession.Connection = toClient;
        authoritySession.Connection = toAuthority;
        return (toAuthority, toClient);
    }

    public ConnectionSendResult TrySendOwned(OutboundPackage message)
    {
        if (!IsConnected || !ReferenceEquals(message.Runtime, _source)) { return ConnectionSendResult.Closed; }

        var payload = message.Package;
        if (payload is TerrainSnapshotMessage snapshot)
        {
            if (!_source.IsAuthority) { return ConnectionSendResult.Closed; }

            if (!ClientChunkContentTransport.TryGet(_destination.Runtime, out var content))
            {
                return ConnectionSendResult.Backpressured;
            }

            if (!content.CanReceive(snapshot.Snapshot.Allocation, snapshot.Snapshot.ContentVersion))
            {
                return ConnectionSendResult.Accepted;
            }

            var reservation = content.TryReserve(snapshot.Snapshot.Allocation);
            if (reservation == null) { return ConnectionSendResult.Backpressured; }

            payload = new TerrainSnapshotMessage(snapshot.Snapshot, reservation);
        }
        else { payload = ProtocolMessageOwnership.Capture(payload); }

        if (WorldNetworkBinding.For(_destination.Runtime).Inbox.TryPostOwned(
                new ReceivedPackage(payload, _destination),
                _lifetime.Token
            )) { return ConnectionSendResult.Accepted; }

        if (!ReferenceEquals(payload, message.Package)) { (payload as IDisposable)?.Dispose(); }

        return IsConnected ? ConnectionSendResult.Backpressured : ConnectionSendResult.Closed;
    }

    public void Dispose()
    {
        _lifetime.Close();
    }

    public void Disconnect(string reason)
    {
        _lifetime.Close(reason);
    }

    private sealed class Lifetime
    {
        private readonly CancellationTokenSource _closed;

        public CancellationToken Token { get; }

        private int _isClosed;

        private readonly Lock _gate = new();

        internal string DisconnectReason { get; private set; } = string.Empty;

        public Lifetime(CancellationToken authorityClosed, CancellationToken clientClosed)
        {
            _closed = CancellationTokenSource.CreateLinkedTokenSource(authorityClosed, clientClosed);
            Token = _closed.Token;
        }

        public bool IsClosed => Volatile.Read(ref _isClosed) != 0 || Token.IsCancellationRequested;

        public void Close(string reason = "")
        {
            lock (_gate)
            {
                if (_isClosed != 0) { return; }

                DisconnectReason = reason;
                Volatile.Write(ref _isClosed, 1);
                _closed.Cancel();
                _closed.Dispose();
            }
        }
    }
}
