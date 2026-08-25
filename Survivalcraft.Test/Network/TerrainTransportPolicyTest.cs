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
