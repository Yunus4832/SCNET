using Game.Network.Serialization;

namespace Survivalcraft.Test.Network;

public sealed class NetworkCompressionTest
{
    [Fact]
    public void AdaptiveCompressionLeavesSmallFramesUncompressed()
    {
        using var writer = new PackageStreamWriter();
        writer.Write(42);

        var frame = writer.Data();

        Assert.Equal(0, frame[0]);
        using var reader = new PackageStreamReader(frame);
        Assert.Equal(42, reader.ReadInt32());
    }

    [Fact]
    public void AdaptiveCompressionCompressesLargeRepetitiveFrames()
    {
        using var writer = new PackageStreamWriter();
        writer.Write(new byte[16 * 1024]);

        var frame = writer.Data();

        Assert.Equal(1, frame[0]);
        Assert.True(frame.Length < 1024);
        using var reader = new PackageStreamReader(frame);
        Assert.Equal(new byte[16 * 1024], reader.ReadBytes(16 * 1024));
    }
}
