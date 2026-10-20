using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Rendezvous.Protocol;

/// <summary>Authenticates a bounded raw transport datagram without exposing its route key.</summary>
public static class RelayDatagramCodec
{
    public const byte CurrentVersion = 1;

    private const int _headerSize = 30;
    private const int _tagSize = 16;
    public const int Overhead = _headerSize + _tagSize;
    public const int MaxPayloadSize = 1472 - Overhead;

    public static byte[] Encode(Guid routeId, byte side, ulong sequence, ReadOnlySpan<byte> key,
        ReadOnlySpan<byte> payload)
    {
        if (routeId == Guid.Empty || side > 1 || sequence == 0 || key.Length != 32 ||
            payload.IsEmpty || payload.Length > MaxPayloadSize)
        {
            throw new ArgumentException("Invalid relay datagram.");
        }
        var result = new byte[Overhead + payload.Length];
        "SCRL"u8.CopyTo(result);
        result[4] = CurrentVersion;
        routeId.TryWriteBytes(result.AsSpan(5, 16), bigEndian: true, out _);
        result[21] = side;
        BinaryPrimitives.WriteUInt64BigEndian(result.AsSpan(22, 8), sequence);
        payload.CopyTo(result.AsSpan(_headerSize));
        var hash = HMACSHA256.HashData(key, result.AsSpan(0, result.Length - _tagSize));
        hash.AsSpan(0, _tagSize).CopyTo(result.AsSpan(result.Length - _tagSize));
        return result;
    }

    public static bool TryReadRoute(ReadOnlySpan<byte> data, out Guid routeId, out byte side)
    {
        routeId = Guid.Empty;
        side = 0;
        if (data.Length <= Overhead || data.Length > Overhead + MaxPayloadSize ||
            !data[..4].SequenceEqual("SCRL"u8) || data[4] != CurrentVersion || data[21] > 1 ||
            BinaryPrimitives.ReadUInt64BigEndian(data.Slice(22, 8)) == 0)
        {
            return false;
        }
        routeId = new Guid(data.Slice(5, 16), bigEndian: true);
        side = data[21];
        return routeId != Guid.Empty;
    }

    public static bool TryDecode(ReadOnlySpan<byte> data, ReadOnlySpan<byte> key, out RelayDatagram? datagram)
    {
        datagram = null;
        if (key.Length != 32 || !TryReadRoute(data, out var routeId, out var side))
        {
            return false;
        }
        var hash = HMACSHA256.HashData(key, data[..^_tagSize]);
        if (!CryptographicOperations.FixedTimeEquals(hash.AsSpan(0, _tagSize), data[^_tagSize..]))
        {
            return false;
        }
        datagram = new RelayDatagram(routeId, side, BinaryPrimitives.ReadUInt64BigEndian(data.Slice(22, 8)),
            data.Slice(_headerSize, data.Length - Overhead).ToArray());
        return true;
    }
}
