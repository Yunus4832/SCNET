using Engine.Core;

using Game.Network;
using Game.Network.Packages;
using Game.Network.Serialization;
using Game.Subsystems;

namespace Survivalcraft.Test.Network;

public sealed class ElectricityBoundaryReplicationTest
{
    [Fact]
    public void LeavingAndReenteringChunksRebuildsLatestAuthorityThroughWireProtocol()
    {
        var client = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid(), null);
        var tracker = new ElectricityReplicationTracker();
        var receiver = new SubsystemElectricity();
        var left = new Point3(-1, 20, 0);
        var right = new Point3(16, 20, 0);
        var authority = new Dictionary<Point3, float> { [left] = 0.5f, [right] = 1f };

        Send(new Point2(-1, 0), 10);
        Send(new Point2(1, 0), 20);
        authority[left] = 0.75f;
        Send(new Point2(-1, 0), 30);

        Assert.Equal(0.5f, Assert.Single(receiver.List[0].SaveData).Value);
        Assert.True(receiver.List[0].SaveData.ContainsKey(left));
        Assert.Equal(1f, Assert.Single(receiver.List[1].SaveData).Value);
        Assert.True(receiver.List[1].SaveData.ContainsKey(right));
        Assert.Equal(0.75f, Assert.Single(receiver.List[2].SaveData).Value);
        Assert.True(receiver.List[2].SaveData.ContainsKey(left));
        Assert.Equal([10, 20, 30], receiver.List.Select(snapshot => snapshot.StartStep));

        void Send(Point2 observedChunk, int step)
        {
            var snapshot = SubsystemElectricity.BuildNetworkSnapshots(step, [client], authority,
                chunk => chunk == observedChunk ? [client] : [])[client];
            var changes = tracker.Capture(client, snapshot.SaveData);
            snapshot.IsBaseline = changes.IsBaseline;
            snapshot.SaveData.Clear();
            foreach (var (point, voltage) in changes.Voltages)
            {
                snapshot.SaveData.Add(point, voltage);
            }

            snapshot.Removed.AddRange(changes.Removed);
            using var writer = new PackageStreamWriter();
            new SubsystemElectricityPackage(snapshot).WriteData(writer);
            using var reader = new PackageStreamReader(writer.Data());
            var received = new SubsystemElectricityPackage();
            received.ReadData(reader);
            receiver.ReceiveNetworkSnapshot(received.Snapshot);
        }
    }
}
