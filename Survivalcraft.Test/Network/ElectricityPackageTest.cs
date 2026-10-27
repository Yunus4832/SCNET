using Engine.Core.Mathematics;

using Game.Network;
using Game.Network.Replication;
using Game.Subsystems;

namespace Survivalcraft.Test.Network;

public sealed class ElectricityPackageTest
{
    [Fact]
    public void QueuedDeltaPayloadIsIndependentOfLaterSimulationTicks()
    {
        var tracker = new ElectricityReplicationTracker();
        var client = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid(), null);
        var voltages = new Dictionary<Point3, float> { [new Point3(1, 2, 3)] = 0.5f };
        var changes = tracker.Capture(client, voltages);
        var package = new SubsystemElectricityPackage(
            new ElectricitySnapshot
            {
                StartStep = 42, IsBaseline = changes.IsBaseline, SaveData = changes.Voltages, Removed = changes.Removed
            }
        );
        var before = ProtocolMessageCodec.Encode(package);
        voltages.Clear();
        _ = tracker.Capture(client, voltages);
        var after = ProtocolMessageCodec.Encode(package);
        Assert.Equal(before, after);
    }

    [Fact]
    public void UnchangedVisibleCircuitSendsOnlyClockAfterBaseline()
    {
        var tracker = new ElectricityReplicationTracker();
        var client = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid(), null);
        var values = Enumerable.Range(0, 1024).ToDictionary(index => new Point3(index, 0, 0), _ => 1f);
        var baseline = tracker.Capture(client, values);
        var snapshot = new SubsystemElectricity.NetSimulate { StartStep = 10, IsBaseline = baseline.IsBaseline };
        foreach (var (point, voltage) in baseline.Voltages)
        {
            snapshot.SaveData[point] = voltage;
        }

        var before =
            ProtocolMessageCodec.Encode(new SubsystemElectricityPackage(ElectricityTestSnapshots.Capture(snapshot)));
        var unchanged = tracker.Capture(client, values);
        var heartbeat = new SubsystemElectricity.NetSimulate { StartStep = 15 };
        var after = ProtocolMessageCodec.Encode(
            new SubsystemElectricityPackage(ElectricityTestSnapshots.Capture(heartbeat))
        );

        Assert.False(unchanged.IsBaseline);
        Assert.Empty(unchanged.Voltages);
        Assert.Empty(unchanged.Removed);
        Assert.True(before.Length > 1024);
        Assert.InRange(after.Length, 1, 32);
        var reader = after;
        var decoded =
            Assert.IsType<SubsystemElectricityPackage>(ProtocolMessageCodec.Decode(reader));
        Assert.Equal(15, decoded.Snapshot.StartStep);
    }

    [Fact]
    public void ClientReconstructsCompleteAuthorityWithoutMutatingQueuedSteps()
    {
        var subsystem = new SubsystemElectricity();
        var point = new Point3(1, 2, 3);
        var baseline = new SubsystemElectricity.NetSimulate
        {
            StartStep = 1, IsBaseline = true,
            SaveData =
            {
                [point] = 0.5f
            }
        };
        subsystem.ReceiveNetworkSnapshot(ElectricityTestSnapshots.Capture(baseline));
        subsystem.ReceiveNetworkSnapshot(
            ElectricityTestSnapshots.Capture(
                new SubsystemElectricity.NetSimulate
                    { StartStep = 2 }
            )
        );
        var removal = new SubsystemElectricity.NetSimulate { StartStep = 3 };
        removal.Removed.Add(point);
        subsystem.ReceiveNetworkSnapshot(ElectricityTestSnapshots.Capture(removal));

        Assert.Equal(0.5f, subsystem.List[0].SaveData[point]);
        Assert.Equal(0.5f, subsystem.List[1].SaveData[point]);
        Assert.Empty(subsystem.List[2].SaveData);
        var replacement = new SubsystemElectricity.NetSimulate { StartStep = 4, IsBaseline = true };
        subsystem.ReceiveNetworkSnapshot(ElectricityTestSnapshots.Capture(replacement));
        Assert.Empty(subsystem.List[3].SaveData);
    }

    [Fact]
    public void BaselineAndRemovalMarkersRoundTrip()
    {
        var snapshot = new SubsystemElectricity.NetSimulate { StartStep = 42, IsBaseline = true };
        snapshot.Removed.Add(new Point3(-1, 2, 3));
        var writer =
            ProtocolMessageCodec.Encode(new SubsystemElectricityPackage(ElectricityTestSnapshots.Capture(snapshot)));
        var reader = writer;
        var clone = Assert.IsType<SubsystemElectricityPackage>(ProtocolMessageCodec.Decode(reader));

        Assert.True(clone.Snapshot.IsBaseline);
        Assert.Equal(snapshot.Removed, clone.Snapshot.Removed);
        Assert.Equal(42, clone.Snapshot.StartStep);
    }

    [Fact]
    public void VoltageCountDoesNotWrapAtUshortBoundary()
    {
        var snapshot = new SubsystemElectricity.NetSimulate { StartStep = 123 };
        for (var i = 0; i <= ushort.MaxValue; i++)
        {
            snapshot.SaveData.Add(new Point3(i, 0, 0), 0.5f);
        }

        var writer =
            ProtocolMessageCodec.Encode(new SubsystemElectricityPackage(ElectricityTestSnapshots.Capture(snapshot)));
        var reader = writer;
        var clone = Assert.IsType<SubsystemElectricityPackage>(ProtocolMessageCodec.Decode(reader));

        Assert.Equal(123, clone.Snapshot.StartStep);
        Assert.Equal(65536, clone.Snapshot.SaveData.Count);
        Assert.Equal(0.5f, clone.Snapshot.SaveData[new Point3(65535, 0, 0)]);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public void InvalidCountIsRejectedBeforeReadingEntries(int count)
    {
        var payload = ProtocolMessageCodec.Encode(new SubsystemElectricityPackage(new ElectricitySnapshot()));
        Assert.Throws<InvalidDataException>(() =>
            ProtocolMessageCodec.Decode(payload.AsMemory(0, Math.Min(count < 0 ? 0 : count, payload.Length - 1)))
        );
    }

    [Fact]
    public void InterestFilteringReducesPayloadWithoutDroppingClock()
    {
        var client = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid(), null);
        var voltages = new Dictionary<Point3, float>();
        for (var i = 0; i < 1024; i++)
        {
            voltages.Add(new Point3(i * 16, 1, 0), 1f);
        }

        var filtered = SubsystemElectricity.BuildNetworkVoltages(
            [client],
            voltages,
            chunk => chunk.X < 16 ? [client] : []
        );
        var global = new SubsystemElectricity.NetSimulate { StartStep = 10 };
        foreach (var (point, voltage) in voltages)
        {
            global.SaveData.Add(point, voltage);
        }

        var before =
            ProtocolMessageCodec.Encode(new SubsystemElectricityPackage(ElectricityTestSnapshots.Capture(global)));
        var after = ProtocolMessageCodec.Encode(
            new SubsystemElectricityPackage(new ElectricitySnapshot { StartStep = 10, SaveData = filtered[client] })
        );
        Assert.True(before.Length > after.Length * 16);
    }
}
