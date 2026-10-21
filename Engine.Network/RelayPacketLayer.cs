using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;

using LiteNetLib.Layers;

using Rendezvous.Protocol;

namespace Engine.Network;

/// <summary>Wraps only assigned relay endpoints; ordinary transport packets remain unchanged.</summary>
internal sealed class RelayPacketLayer() : PacketLayerBase(RelayDatagramCodec.Overhead)
{
    private readonly ConcurrentDictionary<IPEndPoint, Route> _routes = new();

    internal void Add(IPEndPoint endpoint, Guid routeId, byte side, ReadOnlySpan<byte> key)
    {
        if (routeId == Guid.Empty || side > 1 || key.Length != 32)
        {
            throw new ArgumentException("Invalid relay route.");
        }

        if (!_routes.TryAdd(endpoint, new Route(routeId, side, key.ToArray())))
        {
            throw new InvalidOperationException("Relay endpoint already assigned.");
        }
    }

    internal bool Contains(IPEndPoint endpoint) => _routes.ContainsKey(endpoint);

    internal bool Matches(IPEndPoint endpoint, Guid routeId, byte side, ReadOnlySpan<byte> key) =>
        _routes.TryGetValue(endpoint, out var route) &&
        route.Id == routeId &&
        route.Side == side &&
        CryptographicOperations.FixedTimeEquals(route.Key, key);

    internal void Remove(IPEndPoint endpoint)
    {
        if (_routes.TryRemove(endpoint, out var route))
        {
            CryptographicOperations.ZeroMemory(route.Key);
        }
    }

    public override void ProcessInboundPacket(IPEndPoint endPoint, ref byte[] data, ref int offset, ref int length)
    {
        if (!_routes.TryGetValue(endPoint, out var route))
        {
            if (data.AsSpan(offset, length).StartsWith("SCRL"u8))
            {
                NetworkDiagnostics.log.RelayPacket(endPoint.ToString(), string.Empty, "unassigned-endpoint", length);
                length = 0;
            }

            return;
        }

        if (!RelayDatagramCodec.TryDecode(data.AsSpan(offset, length), route.Key, out var packet) ||
            packet is null ||
            packet.RouteId != route.Id ||
            packet.Side != route.Side)
        {
            NetworkDiagnostics.log.RelayPacket(
                endPoint.ToString(),
                route.Id.ToString(),
                "invalid-authentication",
                length
            );
            length = 0;
            return;
        }

        lock (route.Replay)
        {
            if (!route.Replay.TryAccept(packet.Sequence))
            {
                NetworkDiagnostics.log.RelayPacket(endPoint.ToString(), route.Id.ToString(), "replayed", length);
                length = 0;
                return;
            }
        }

        NetworkDiagnostics.log.RelayPacket(endPoint.ToString(), route.Id.ToString(), "accepted", length);
        data = packet.Payload;
        offset = 0;
        length = data.Length;
    }

    public override void ProcessOutBoundPacket(IPEndPoint endPoint, ref byte[] data, ref int offset, ref int length)
    {
        if (!_routes.TryGetValue(endPoint, out var route))
        {
            return;
        }

        var sequence = unchecked((ulong)Interlocked.Increment(ref route.sequence));
        data = RelayDatagramCodec.Encode(route.Id, route.Side, sequence, route.Key, data.AsSpan(offset, length));
        offset = 0;
        length = data.Length;
    }

    private sealed class Route(Guid id, byte side, byte[] key)
    {
        public Guid Id { get; } = id;

        public byte Side { get; } = side;

        public byte[] Key { get; } = key;

        public ReplayWindow Replay { get; } = new();

        internal long sequence;
    }
}
