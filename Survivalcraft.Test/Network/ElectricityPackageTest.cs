using Engine.Core;

using Game.Network;
using Game.Network.Packages;
using Game.Network.Serialization;
using Game.Subsystems;

namespace Survivalcraft.Test.Network;

public sealed class ElectricityPackageTest
{
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

        using var before = new PackageStreamWriter();
        new SubsystemElectricityPackage(snapshot).WriteData(before);
        var unchanged = tracker.Capture(client, values);
        using var after = new PackageStreamWriter();
        var heartbeat = new SubsystemElectricity.NetSimulate { StartStep = 15 };
        new SubsystemElectricityPackage(heartbeat).WriteData(after);

        Assert.False(unchanged.IsBaseline);
        Assert.Empty(unchanged.Voltages);
        Assert.Empty(unchanged.Removed);
        Assert.Equal(16397, before.BaseStream.Length);
        Assert.Equal(13, after.BaseStream.Length);
        using var reader = new PackageStreamReader(after.Data());
        var decoded = new SubsystemElectricityPackage();
        decoded.ReadData(reader);
        Assert.Equal(15, decoded.Snapshot.StartStep);
    }

    [Fact]
    public void ClientReconstructsCompleteAuthorityWithoutMutatingQueuedSteps()
    {
        var subsystem = new SubsystemElectricity();
        var point = new Point3(1, 2, 3);
        var baseline = new SubsystemElectricity.NetSimulate { StartStep = 1, IsBaseline = true };
        baseline.SaveData[point] = 0.5f;
        subsystem.ReceiveNetworkSnapshot(baseline);
        subsystem.ReceiveNetworkSnapshot(new SubsystemElectricity.NetSimulate { StartStep = 2 });
        var removal = new SubsystemElectricity.NetSimulate { StartStep = 3 };
        removal.Removed.Add(point);
        subsystem.ReceiveNetworkSnapshot(removal);

        Assert.Equal(0.5f, subsystem.List[0].SaveData[point]);
        Assert.Equal(0.5f, subsystem.List[1].SaveData[point]);
        Assert.Empty(subsystem.List[2].SaveData);
        var replacement = new SubsystemElectricity.NetSimulate { StartStep = 4, IsBaseline = true };
        subsystem.ReceiveNetworkSnapshot(replacement);
        Assert.Empty(subsystem.List[3].SaveData);
    }

    [Fact]
    public void BaselineAndRemovalMarkersRoundTrip()
    {
        var snapshot = new SubsystemElectricity.NetSimulate { StartStep = 42, IsBaseline = true };
        snapshot.Removed.Add(new Point3(-1, 2, 3));
        using var writer = new PackageStreamWriter();
        new SubsystemElectricityPackage(snapshot).WriteData(writer);
        using var reader = new PackageStreamReader(writer.Data());
        var clone = new SubsystemElectricityPackage();
        clone.ReadData(reader);

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

        using var writer = new PackageStreamWriter();
        new SubsystemElectricityPackage(snapshot).WriteData(writer);
        using var reader = new PackageStreamReader(writer.Data());
        var clone = new SubsystemElectricityPackage();
        clone.ReadData(reader);

        Assert.Equal(123, clone.Snapshot.StartStep);
        Assert.Equal(65536, clone.Snapshot.SaveData.Count);
        Assert.Equal(0.5f, clone.Snapshot.SaveData[new Point3(65535, 0, 0)]);
        Assert.Equal(reader.BaseStream.Length, reader.BaseStream.Position);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public void InvalidCountIsRejectedBeforeReadingEntries(int count)
    {
        using var writer = new PackageStreamWriter();
        writer.Write(100);
        writer.Write(false);
        writer.Write(count);
        using var reader = new PackageStreamReader(writer.Data());

        Assert.Throws<InvalidDataException>(() => new SubsystemElectricityPackage().ReadData(reader));
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

        var filtered = SubsystemElectricity.BuildNetworkSnapshots(10, [client], voltages,
            chunk => chunk.X < 16 ? [client] : []);
        var global = new SubsystemElectricity.NetSimulate { StartStep = 10 };
        foreach (var (point, voltage) in voltages)
        {
            global.SaveData.Add(point, voltage);
        }

        using var before = new PackageStreamWriter();
        using var after = new PackageStreamWriter();
        new SubsystemElectricityPackage(global).WriteData(before);
        new SubsystemElectricityPackage(filtered[client]).WriteData(after);

        Assert.Equal(16397, before.BaseStream.Length);
        Assert.Equal(269, after.BaseStream.Length);
        Assert.Equal(10, filtered[client].StartStep);
    }
}
