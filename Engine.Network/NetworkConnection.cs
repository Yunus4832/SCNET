using System.Net;

using LiteNetLib;

namespace Engine.Network;

/// <summary>A transport connection whose identity is independent of its remote address.</summary>
public sealed class NetworkConnection
{
    private readonly NetPeer _peer;
    private readonly int _maxPendingBytes;
    private readonly byte _channelCount;
    private readonly Dictionary<Guid, PendingDelivery> _pending = new();
    private readonly Lock _sendGate = new();
    private int _pendingBytes;

    internal NetworkConnection(NetPeer peer, int maxPendingBytes, byte channelCount)
    {
        _peer = peer;
        _maxPendingBytes = maxPendingBytes;
        _channelCount = channelCount;
    }

    public Guid Id { get; } = Guid.NewGuid();

    public IPEndPoint TransportEndpoint => new(_peer.EndPoint.Address, _peer.EndPoint.Port);

    public NetworkPathKind PathKind { get; internal set; } = NetworkPathKind.Direct;

    public bool IsConnected => _peer.ConnectionState == ConnectionState.Connected;

    public int RoundTripMilliseconds => _peer.Ping;

    public double PacketLossPercent => _peer.Statistics.PacketLossPercent;

    public int PendingReliableBytes => Volatile.Read(ref _pendingBytes);

    public NetworkSendResult TrySend(ReadOnlySpan<byte> payload, byte channel = 0,
        DeliveryMode delivery = DeliveryMode.ReliableOrdered) => SendCore(payload, channel, delivery, null);

    public void Send(ReadOnlySpan<byte> payload, byte channel = 0, DeliveryMode delivery = DeliveryMode.ReliableOrdered)
    {
        var result = TrySend(payload, channel, delivery);
        if (result != NetworkSendResult.Accepted)
        {
            throw new NetworkSendException(result);
        }
    }

    public DeliveryReceipt SendWithReceipt(ReadOnlySpan<byte> payload, byte channel = 0,
        DeliveryMode delivery = DeliveryMode.ReliableOrdered)
    {
        if (delivery is not (DeliveryMode.ReliableOrdered or DeliveryMode.ReliableUnordered))
        {
            throw new ArgumentException("Transport confirmation requires reliable delivery.", nameof(delivery));
        }
        var receipt = new DeliveryReceipt();
        var result = SendCore(payload, channel, delivery, receipt);
        if (result != NetworkSendResult.Accepted)
        {
            throw new NetworkSendException(result);
        }
        return receipt;
    }

    private NetworkSendResult SendCore(ReadOnlySpan<byte> payload, byte channel, DeliveryMode delivery,
        DeliveryReceipt? receipt)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(channel, _channelCount);
        var method = ToDeliveryMethod(delivery);
        lock (_sendGate)
        {
            if (!IsConnected)
            {
                return NetworkSendResult.Closed;
            }
            if (delivery is DeliveryMode.Sequenced or DeliveryMode.Unreliable)
            {
                if (payload.Length > _peer.GetMaxSinglePacketSize(method))
                {
                    return NetworkSendResult.TooLarge;
                }
                _peer.Send(payload.ToArray(), channel, method);
                return NetworkSendResult.Accepted;
            }
            if (payload.Length > _maxPendingBytes)
            {
                return NetworkSendResult.TooLarge;
            }
            if (_pending.Count >= 1024 || payload.Length > _maxPendingBytes - _pendingBytes)
            {
                return NetworkSendResult.Backpressure;
            }
            var pending = new PendingDelivery(payload.Length, receipt);
            _pending.Add(pending.Id, pending);
            _pendingBytes += pending.Bytes;
            try
            {
                _peer.SendWithDeliveryEvent(payload.ToArray(), channel, method, pending);
                return NetworkSendResult.Accepted;
            }
            catch
            {
                _pending.Remove(pending.Id);
                _pendingBytes -= pending.Bytes;
                throw;
            }
        }
    }

    internal void ConfirmDelivery(PendingDelivery delivery)
    {
        lock (_sendGate)
        {
            if (_pending.Remove(delivery.Id, out var pending) && ReferenceEquals(pending, delivery))
            {
                _pendingBytes -= pending.Bytes;
                pending.Receipt?.Confirm();
            }
        }
    }

    internal void FailPendingDeliveries()
    {
        lock (_sendGate)
        {
            foreach (var pending in _pending.Values)
            {
                pending.Receipt?.Fail();
            }
            _pending.Clear();
            _pendingBytes = 0;
        }
    }

    public int GetMaxPayloadSize(DeliveryMode delivery) => _peer.GetMaxSinglePacketSize(ToDeliveryMethod(delivery));

    private static DeliveryMethod ToDeliveryMethod(DeliveryMode delivery) => delivery switch
    {
        DeliveryMode.ReliableOrdered => DeliveryMethod.ReliableOrdered,
        DeliveryMode.ReliableUnordered => DeliveryMethod.ReliableUnordered,
        DeliveryMode.Sequenced => DeliveryMethod.Sequenced,
        DeliveryMode.Unreliable => DeliveryMethod.Unreliable,
        _ => throw new ArgumentOutOfRangeException(nameof(delivery))
    };

    public void Disconnect(ReadOnlySpan<byte> reason = default)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(reason.Length, 256);
        _peer.Disconnect(reason.ToArray());
    }
}
