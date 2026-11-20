using Game.Network.Chunks;
using Game.Terrains;

namespace Survivalcraft.Core.Test.Network;

public sealed class NetworkChunkSnapshotTest
{
    [Fact]
    public void CaptureOwnsIndependentContentsAndReleasesItsPoolLeaseExactlyOnce()
    {
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        chunk.SetCellValueFast(0, 7);
        chunk.SetTemperatureFast(0, 0, 5);
        var revision = chunk.NetworkContentRevision;
        var snapshot = Assert.IsType<NetworkChunkSnapshot>(NetworkChunkSnapshot.TryCapture(chunk));
        chunk.SetCellValueFast(0, 9);
        chunk.SetTemperatureFast(0, 0, 8);

        Assert.Same(chunk, snapshot.Source);
        Assert.Equal(revision, snapshot.Revision);
        Assert.Equal(7, snapshot.Cells[0]);
        Assert.Equal(5, Terrain.ExtractTemperature(snapshot.Shafts[0]));
        snapshot.Dispose();
        snapshot.Dispose();
        Assert.Throws<ObjectDisposedException>(() => snapshot.Cells);
        Assert.Throws<ObjectDisposedException>(() => snapshot.Shafts);
    }
}
