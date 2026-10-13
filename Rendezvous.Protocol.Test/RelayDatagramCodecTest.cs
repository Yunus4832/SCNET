using System.Security.Cryptography;

namespace Rendezvous.Protocol.Test;

public sealed class RelayDatagramCodecTest
{
    [Fact]
    public void RoundTripPreservesDatagramAndRejectsWrongKeyOrModifiedHeaderAndPayload()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var route = Guid.NewGuid();
        var encoded = RelayDatagramCodec.Encode(route, 1, 42, key, "transport-datagram"u8);
        Assert.True(RelayDatagramCodec.TryDecode(encoded, key, out var decoded));
        Assert.Equal(route, decoded!.RouteId);
        Assert.Equal(1, decoded.Side);
        Assert.Equal(42UL, decoded.Sequence);
        Assert.Equal("transport-datagram"u8.ToArray(), decoded.Payload);
        Assert.False(RelayDatagramCodec.TryDecode(encoded, RandomNumberGenerator.GetBytes(32), out _));
        foreach (var offset in new[] { 5, 21, 22, 30, encoded.Length - 1 })
        {
            var modified = encoded.ToArray();
            modified[offset] ^= 1;
            Assert.False(RelayDatagramCodec.TryDecode(modified, key, out _));
        }
    }

    [Fact]
    public void InvalidLengthsAndOversizedDatagramsAreRejected()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        for (var size = 0; size <= RelayDatagramCodec.Overhead; size++)
        {
            Assert.False(RelayDatagramCodec.TryDecode(new byte[size], key, out _));
        }
        Assert.Throws<ArgumentException>(() => RelayDatagramCodec.Encode(Guid.NewGuid(), 0, 1, key,
            new byte[RelayDatagramCodec.MaxPayloadSize + 1]));
        var encoded = RelayDatagramCodec.Encode(Guid.NewGuid(), 0, 1, key, new byte[RelayDatagramCodec.MaxPayloadSize]);
        Assert.True(RelayDatagramCodec.TryDecode(encoded, key, out _));
        encoded[4] = 2;
        Assert.False(RelayDatagramCodec.TryDecode(encoded, key, out _));
    }

    [Fact]
    public void ReplayWindowAllowsReorderingButNotDuplicatesOrOldPackets()
    {
        var window = new ReplayWindow();
        Assert.False(window.TryAccept(0));
        Assert.True(window.TryAccept(100));
        Assert.True(window.TryAccept(99));
        Assert.False(window.TryAccept(99));
        Assert.True(window.TryAccept(37));
        Assert.False(window.TryAccept(36));
        Assert.True(window.TryAccept(164));
        Assert.False(window.TryAccept(100));
        Assert.True(window.TryAccept(ulong.MaxValue));
        Assert.False(window.TryAccept(164));
        Assert.False(window.TryAccept(ulong.MaxValue));
    }
}
