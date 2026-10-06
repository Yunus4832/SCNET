using Engine.Core;

using Game.Network;
using Game.Network.Packages;
using Game.Network.Serialization;
using Game.Terrains.Distribution;

using LiteNetLib;

namespace Survivalcraft.Test.Network;

public sealed class TerrainTransportPolicyTest
{
    [Fact]
    public void TransportChannelsAreContiguousAndCoveredByNetManager()
    {
        var channels = Enum.GetValues<NetworkChannel>();
        Assert.Equal(Enumerable.Range(0, channels.Length).Select(index => (byte)index),
            channels.Select(channel => (byte)channel));
        var node = new NetNode();
        Assert.Equal(channels.Length, node.NetManager.ChannelsCount);
    }

    [Fact]
    public void FullFragmentFitsInitialSafeMtuWithoutCompression()
    {
        var allocation = new ChunkAllocationId(new Point2(int.MinValue, int.MaxValue), ulong.MaxValue);
        var chunk = new EncodedTerrainChunk(allocation.Coords, long.MaxValue,
            new byte[EncodedTerrainChunkFragmenter.DefaultFragmentPayloadSize]);
        var fragment = Assert.Single(EncodedTerrainChunkFragmenter.Split(chunk, allocation));
        var package = new SubsystemTerrainPackage(fragment);
        using var writer = new PackageStreamWriter();
        writer.Write((byte)0);
        writer.Write(package.ID);
        package.WriteData(writer);

        var datagram = CommonLib.GetWriter(writer, out _, CommonLib.CompressionPolicy.None);

        Assert.InRange(datagram.Length, 1, 507);
    }

    [Fact]
    public void ChunkFragmentsUseIndependentUnreliableDatagrams()
    {
        var coords = new Point2(1, 2);
        var package = new SubsystemTerrainPackage(new EncodedTerrainChunkFragment(
            new ChunkAllocationId(coords, 1), 1, 3, 0, 1, [1, 2, 3]));

        var transport = PackageTransportPolicy.Get(package);

        Assert.Equal(NetworkChannel.TerrainFragment, transport.Channel);
        Assert.Equal(DeliveryMethod.Unreliable, transport.DeliveryMethod);
    }

    [Fact]
    public void ChunkRequestsRemainReliableOrderedControlMessages()
    {
        var package = new SubsystemTerrainPackage([
            new ChunkContentRequest(new ChunkAllocationId(new Point2(1, 2), 1))
        ]);

        Assert.Equal(PackageTransportPolicy.Control, PackageTransportPolicy.Get(package));
    }

    [Fact]
    public void CellDeltasUseReliableOrderedControlMessages()
    {
        var package = new SubsystemTerrainPackage(
            new TerrainCellDelta(new Point3(1, 2, 3), 4, 5, 6));

        var transport = PackageTransportPolicy.Get(package);

        Assert.Equal(NetworkChannel.Control, transport.Channel);
        Assert.Equal(DeliveryMethod.ReliableOrdered, transport.DeliveryMethod);
    }
}
