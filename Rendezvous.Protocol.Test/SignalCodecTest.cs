namespace Rendezvous.Protocol.Test;

public sealed class SignalCodecTest
{
    [Fact]
    public void ControlRoundTripsWithIdentityAndChallenge()
    {
        var message = new SignalMessage("confirm", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new string('A', 64), Challenge: new string('B', 32));
        Assert.True(SignalCodec.TryDecode(SignalCodec.Encode(message), out var decoded));
        Assert.Equal(message, decoded);
    }

    [Fact]
    public void UnknownVersionTruncationAndApplicationBytesAreRejected()
    {
        var bytes = SignalCodec.Encode(new SignalMessage("register", Guid.NewGuid()));
        for (var length = 0; length < bytes.Length; length++)
        {
            Assert.False(SignalCodec.TryDecode(bytes.AsSpan(0, length), out _));
        }

        bytes[4] = 2;
        Assert.False(SignalCodec.TryDecode(bytes, out _));
        Assert.False(SignalCodec.TryDecode("application-payload"u8, out _));
    }

    [Fact]
    public void OversizedAndMissingIdentityAreRejected()
    {
        Assert.Throws<ArgumentException>(() => SignalCodec.Encode(
            new SignalMessage("register", Guid.NewGuid(), Ticket: new string('A', 2000))));
        Assert.False(SignalCodec.TryDecode(new byte[SignalCodec.MaxDatagramBytes + 1], out _));
        Assert.False(SignalCodec.TryDecode(SignalCodec.Encode(new SignalMessage("register", Guid.Empty)), out _));
    }
}
