using Engine.Core.Mathematics;

using Game.Network;
using Game.Network.Replication;
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
            var voltages = SubsystemElectricity.BuildNetworkVoltages(
                [client],
                authority,
                chunk => chunk == observedChunk ? [client] : []
            )[client];
            var changes = tracker.Capture(client, voltages);
            var snapshot = new ElectricitySnapshot
            {
                StartStep = step, IsBaseline = changes.IsBaseline, SaveData = changes.Voltages,
                Removed = changes.Removed
            };
            var writer = ProtocolMessageCodec.Encode(new SubsystemElectricityPackage(snapshot));
            var reader = writer;
            var received =
                Assert.IsType<SubsystemElectricityPackage>(ProtocolMessageCodec.Decode(reader));
            receiver.ReceiveNetworkSnapshot(received.Snapshot);
        }
    }
}
