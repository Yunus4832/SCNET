using Engine.Network;

using Game.Network.Chunks;
using Game.Terrains.Distribution;

namespace Game.Network.Transport;

internal sealed class NetworkMessageConnection : IMessageConnection
{
    private readonly NetNode _node;

    private readonly Client _recipient;

    private readonly NetworkConnection _peer;

    private readonly WorldRuntimeContext _runtime;

    private readonly CancellationTokenSource _closed;

    private NativeTerrainReceiver? _receiver;

    private int _disconnected;

    internal NetworkMessageConnection(NetNode node, Client recipient, NetworkConnection peer)
    {
        _node = node;
        _recipient = recipient;
        _peer = peer;
        _runtime = node.Runtime;
        _closed = CancellationTokenSource.CreateLinkedTokenSource(_runtime.Lifetime);
        Lifetime = _closed.Token;
    }

    internal CancellationToken Lifetime { get; }

    internal NativeTerrainReceiver? Receiver => _receiver;

    public bool IsConnected => ReferenceEquals(_node.Runtime, _runtime) &&
                               !_runtime.IsClosed &&
                               !_node.IsClosing &&
                               !Lifetime.IsCancellationRequested &&
                               _peer.IsConnected;

    public bool IsInProcess => false;

    private NativeTerrainReceiver? GetReceiver()
    {
        if (_receiver != null) { return _receiver; }

        if (_runtime.IsAuthority || !ClientChunkContentTransport.TryGet(_runtime, out var content))
        {
            return null;
        }

        return _receiver = new NativeTerrainReceiver(_runtime, content, Lifetime);
    }

    internal ReceivedPackage? NormalizeIncoming(ReceivedPackage received)
    {
        if (_runtime.IsClosed ||
            Lifetime.IsCancellationRequested ||
            !ReferenceEquals(_node.Runtime, _runtime) ||
            !ReferenceEquals(received.Context.Runtime, _runtime)) { return null; }

        if (received.Package is TerrainFragmentPackage fragment)
        {
            var message = GetReceiver()?.Receive(fragment.ChunkFragment);
            return message == null ? null : new ReceivedPackage(message, received.Context);
        }

        if (received.Package is not TerrainFragmentsRequestPackage repairs) { return received; }

        if (!_runtime.IsAuthority ||
            _recipient.ConnectionPhase != ConnectionPhase.Live ||
            _recipient.State < ClientState.ProjectLoaded) { return null; }

        var fallback = repairs.FragmentRequests.Where(request =>
                !_node.IsCurrentChunkAllocation(_recipient, request.Allocation) ||
                !_node.TerrainDelivery.RequestMissing(_recipient, request)
            )
            .Select(request => new ChunkContentRequest(request.Allocation, request.ContentVersion)).ToArray();
        return fallback.Length == 0
            ? null
            : new ReceivedPackage(new TerrainChunksRequestPackage { ChunkRequests = fallback }, received.Context);
    }

    public ConnectionSendResult TrySendOwned(OutboundPackage message)
    {
        if (!IsConnected || !ReferenceEquals(message.Runtime, _runtime)) { return ConnectionSendResult.Closed; }

        if (message.Package is TerrainSnapshotMessage)
        {
            return _runtime.IsAuthority
                ? _node.TerrainDelivery.TryAccept(_recipient, message)
                : ConnectionSendResult.Closed;
        }

        if (!_runtime.IsAuthority && message.Package is TerrainChunksRequestPackage request)
        {
            _node.QueueAdaptedNetworkPackages(_recipient, message, GetReceiver()?.AdaptRequests(request) ?? []);
            return ConnectionSendResult.Accepted;
        }

        return _node.QueueNetworkPackage(_recipient, message)
            ? ConnectionSendResult.Accepted
            : ConnectionSendResult.Closed;
    }

    public void Disconnect(string reason)
    {
        if (Interlocked.Exchange(ref _disconnected, 1) != 0) { return; }

        _closed.Cancel();
        _receiver?.Dispose();
        _closed.Dispose();
        _node.ReleaseConnectionDelivery(_recipient);
        if (_peer.IsConnected)
        {
            _peer.Disconnect(string.IsNullOrEmpty(reason) ? default : NetNode.EncodeDisconnectReason(reason));
        }
    }

    public void Dispose() => Disconnect(string.Empty);
}
