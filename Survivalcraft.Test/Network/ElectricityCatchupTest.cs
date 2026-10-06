using System.Reflection;

using Engine.Core;

using Game;
using Game.ElectricElements;
using Game.Network;
using Game.Network.Enums;
using Game.Network.Packages;
using Game.Network.Serialization;
using Game.Subsystems;

using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Network;

[Collection(ConfigFileCollection.Name)]
public sealed class ElectricityCatchupTest : IDisposable
{
    private readonly WorkType _previousWorkType = CommonLib.WorkType;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AuthorityRestoresExistingOutputAtItsStepWithoutInputTransition(bool randomGenerator)
    {
        CommonLib.WorkType = WorkType.Client;
        var electricity = new SubsystemElectricity();
        var point = new Point3(1, 20, 3);
        var face = new CellFace(point.X, point.Y, point.Z, 4);
        ElectricElement element = randomGenerator
            ? new RandomGeneratorElectricElement(electricity, face)
            : new SRLatchElectricElement(electricity, face);
        var elements = (Dictionary<CellFace, ElectricElement>)typeof(SubsystemElectricity)
            .GetField("_electricElementsByCellFace", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(electricity)!;
        elements.Add(face, element);
        var neighbor = new SRLatchElectricElement(electricity, new CellFace(2, 20, 3, 4));
        element.Connections.Add(new ElectricConnection
        {
            ConnectorType = ElectricConnectorType.Output,
            NeighborConnectorType = ElectricConnectorType.Input,
            NeighborElectricElement = neighbor
        });
        var snapshot = new SubsystemElectricity.NetSimulate { StartStep = 0, IsBaseline = true };
        snapshot.SaveData[point] = 1f;
        electricity.ReceiveNetworkSnapshot(snapshot);

        Assert.Equal(0f, element.GetOutputVoltage(0));
        electricity.Update(0.001f);

        Assert.Equal(1f, electricity.ReadPersistentVoltage(point));
        Assert.Equal(1f, element.GetOutputVoltage(0));
        var scheduled = (Dictionary<int, Dictionary<ElectricElement, bool>>)typeof(SubsystemElectricity)
            .GetField("_futureSimulateLists", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(electricity)!;
        Assert.Contains(neighbor, scheduled[1].Keys);
        Assert.False(element.RestorePersistentVoltage(1f));
        Assert.True(element.RestorePersistentVoltage(0f));
        Assert.Equal(0f, element.GetOutputVoltage(0));
    }

    [Fact]
    public void WireDeltasApplyAtTheirStepsAndRecoverAfterInterestReentry()
    {
        CommonLib.WorkType = WorkType.Client;
        var electricity = new SubsystemElectricity();
        var tracker = new ElectricityReplicationTracker();
        var client = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid(), null);
        var point = new Point3(-1, 20, -17);
        var values = new Dictionary<Point3, float> { [point] = 0.5f };
        Receive(electricity, tracker.Capture(client, values), 0);
        values[point] = 1f;
        Receive(electricity, tracker.Capture(client, values), 10);
        Receive(electricity, tracker.Capture(client, new Dictionary<Point3, float>()), 20);
        Receive(electricity, tracker.Capture(client, values), 30);

        electricity.Update(0.001f);
        Assert.Equal(0.5f, electricity.ReadPersistentVoltage(point));
        electricity.Update(0.001f);
        Assert.Equal(1f, electricity.ReadPersistentVoltage(point));
        electricity.Update(0.001f);
        Assert.Null(electricity.ReadPersistentVoltage(point));
        electricity.Update(0.001f);
        Assert.Equal(1f, electricity.ReadPersistentVoltage(point));
        Assert.Empty(electricity.List);
    }

    [Fact]
    public void LateDeltaStillCorrectsAuthorityAtNextHeartbeat()
    {
        CommonLib.WorkType = WorkType.Client;
        var electricity = new SubsystemElectricity();
        electricity.List.Add(new SubsystemElectricity.NetSimulate { StartStep = 9 });
        electricity.Update(0.001f);
        var point = new Point3(0, 1, 2);
        var late = new SubsystemElectricity.NetSimulate { StartStep = 5, IsBaseline = true };
        late.SaveData[point] = 1f;
        electricity.ReceiveNetworkSnapshot(late);
        electricity.ReceiveNetworkSnapshot(new SubsystemElectricity.NetSimulate { StartStep = 10 });

        electricity.Update(0.001f);

        Assert.Equal(1f, electricity.ReadPersistentVoltage(point));
        Assert.Empty(electricity.List);
    }

    private static void Receive(SubsystemElectricity electricity, ElectricityVoltageChanges changes, int step)
    {
        var snapshot = new SubsystemElectricity.NetSimulate { StartStep = step, IsBaseline = changes.IsBaseline };
        foreach (var (point, voltage) in changes.Voltages)
        {
            snapshot.SaveData[point] = voltage;
        }

        snapshot.Removed.AddRange(changes.Removed);
        using var writer = new PackageStreamWriter();
        new SubsystemElectricityPackage(snapshot).WriteData(writer);
        using var reader = new PackageStreamReader(writer.Data());
        var received = new SubsystemElectricityPackage();
        received.ReadData(reader);
        electricity.ReceiveNetworkSnapshot(received.Snapshot);
    }

    [Fact]
    public void LargeClockGapDoesNotConsumeUnboundedStepsInOneFrame()
    {
        CommonLib.WorkType = WorkType.Client;
        var electricity = new SubsystemElectricity();
        var snapshot = new SubsystemElectricity.NetSimulate { StartStep = 1_000_000 };
        electricity.List.Add(snapshot);

        electricity.Update(0.1f);

        Assert.InRange(electricity.CircuitStep, 1, 10);
        Assert.Same(snapshot, Assert.Single(electricity.List));
    }

    [Fact]
    public void PendingSnapshotSurvivesUntilItsStepIsReached()
    {
        CommonLib.WorkType = WorkType.Client;
        var electricity = new SubsystemElectricity();
        electricity.List.Add(new SubsystemElectricity.NetSimulate { StartStep = 30 });

        electricity.Update(0.1f);
        Assert.Single(electricity.List);
        for (var i = 0; i < 5; i++)
        {
            electricity.Update(0.001f);
        }

        Assert.Equal(31, electricity.CircuitStep);
        Assert.Empty(electricity.List);
    }

    public void Dispose()
    {
        CommonLib.WorkType = _previousWorkType;
    }
}
