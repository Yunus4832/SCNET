namespace Survivalcraft.Test.Network;

public sealed class NetworkCompressionTest
{
    [Theory]
    [InlineData(4, false)]
    [InlineData(16384, true)]
    public void AdaptiveCompressionUsesThePayloadSize(int size, bool compressed)
    {
        var payload = new byte[size];
        using var stream = new MemoryStream(payload);
        var frame = ProtocolFrameCodec.EncodeFrame(stream, CompressionPolicy.Adaptive);
        Assert.Equal(compressed ? 1 : 0, frame[0]);
        Assert.Equal(payload, ProtocolFrameCodec.DecodeFrame(frame));
        if (compressed) { Assert.True(frame.Length < 1024); }
    }
}
