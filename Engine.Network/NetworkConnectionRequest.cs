using System.Net;

using LiteNetLib;

namespace Engine.Network;

/// <summary>A bounded pending handshake that the application explicitly accepts or rejects.</summary>
public sealed class NetworkConnectionRequest
{
    private readonly ConnectionRequest _request;
    private readonly Func<NetPeer, NetworkConnection> _accepted;
    private readonly Lock _transportGate;
    private int _decided;

    internal NetworkConnectionRequest(ConnectionRequest request, TimeSpan timeout, Func<NetPeer, NetworkConnection> accepted, Lock transportGate)
    {
        _request = request;
        _accepted = accepted;
        _transportGate = transportGate;
        ExpiresAt = DateTimeOffset.UtcNow + timeout;
        Handshake = request.Data.AvailableBytes == 0 ? Array.Empty<byte>() : request.Data.GetRemainingBytes();
    }

    public Guid Id { get; } = Guid.NewGuid();

    public IPEndPoint TransportEndpoint => new(_request.RemoteEndPoint.Address, _request.RemoteEndPoint.Port);

    public ReadOnlyMemory<byte> Handshake { get; }

    public DateTimeOffset ExpiresAt { get; }

    public bool IsPending => Volatile.Read(ref _decided) == 0 && ExpiresAt > DateTimeOffset.UtcNow;

    public NetworkConnection? Accept()
    {
        lock (_transportGate)
        {
            Expire();
            if (Interlocked.CompareExchange(ref _decided, 1, 0) != 0)
            {
                return null;
            }
            var peer = _request.Accept();
            return peer is null ? null : _accepted(peer);
        }
    }

    public bool Reject(ReadOnlySpan<byte> reason = default)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(reason.Length, 256);
        lock (_transportGate)
        {
            if (Interlocked.CompareExchange(ref _decided, 1, 0) != 0)
            {
                return false;
            }
            _request.Reject(reason.ToArray());
            return true;
        }
    }

    internal void Expire()
    {
        if (ExpiresAt <= DateTimeOffset.UtcNow)
        {
            Reject("request-expired"u8);
        }
    }
}
