using Engine.Core;

using Game.Network.Serialization;
using Game.Terrains.Distribution;

namespace Survivalcraft.Test.Terrains.Distribution;

public sealed class NetworkChunkContentTransportTest
{
    [Fact]
    public void ReceiveQueuesSnapshotsAndFailuresUntilUpdateLoopDrainsThem()
    {
        var transport = new NetworkChunkContentTransport();
        var allocation = new ChunkAllocationId(new Point2(7, 8), 4);
        var snapshot = new AuthorityChunkSnapshot(
            allocation.Coords,
            2,
            new int[AuthorityChunkSnapshot.CellCount],
            new long[AuthorityChunkSnapshot.ShaftCount]);
        var encoded = NetworkChunkCodec.Encode(snapshot);

        foreach (var fragment in EncodedTerrainChunkFragmenter.Split(encoded, allocation))
        {
            transport.Receive(fragment);
        }
        transport.ReceiveFailures([allocation]);
        var delta = new TerrainCellDelta(new Point3(1, 2, 3), 4, 2, 3);
        transport.Receive(delta);
        var snapshots = new List<ClientChunkSnapshot>();
        var failures = new List<ChunkAllocationId>();
        var deltas = new List<TerrainCellDelta>();

        Assert.Equal(1, transport.DrainReceived(snapshots));
        Assert.Equal(1, transport.DrainDeltas(deltas));
        Assert.Equal(1, transport.DrainFailed(failures));
        Assert.Equal(snapshot.ContentVersion, snapshots[0].ContentVersion);
        Assert.Equal(delta, deltas[0]);
        Assert.Equal(allocation, failures[0]);
        Assert.Equal(0, transport.DrainReceived(snapshots));
        Assert.Equal(0, transport.DrainDeltas(deltas));
        Assert.Equal(0, transport.DrainFailed(failures));
    }
}
