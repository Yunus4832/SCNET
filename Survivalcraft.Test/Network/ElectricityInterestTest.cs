using Engine.Core;

using Game.Network;
using Game.Subsystems;

namespace Survivalcraft.Test.Network;

public sealed class ElectricityInterestTest
{
    [Fact]
    public void SnapshotsKeepClockButOnlyContainObservedChunks()
    {
        var near = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid(), null);
        var far = new Client(null, 2, Guid.NewGuid(), Guid.NewGuid(), null);
        var empty = new Client(null, 3, Guid.NewGuid(), Guid.NewGuid(), null);
        var negativePoint = new Point3(-1, 20, -17);
        var farPoint = new Point3(160, 30, 160);
        var voltages = new Dictionary<Point3, float>
        {
            [negativePoint] = 0.5f,
            [farPoint] = 1f
        };

        var snapshots = SubsystemElectricity.BuildNetworkSnapshots(123, [near, far, empty], voltages,
            chunk => chunk == new Point2(-1, -2) ? [near] : [far]);

        Assert.Equal(3, snapshots.Count);
        Assert.All(snapshots.Values, snapshot => Assert.Equal(123, snapshot.StartStep));
        Assert.Equal(0.5f, Assert.Single(snapshots[near].SaveData).Value);
        Assert.True(snapshots[near].SaveData.ContainsKey(negativePoint));
        Assert.True(snapshots[far].SaveData.ContainsKey(farPoint));
        Assert.Empty(snapshots[empty].SaveData);
        voltages[negativePoint] = 0f;
        Assert.Equal(0.5f, snapshots[near].SaveData[negativePoint]);
    }

    [Fact]
    public void ObserversOutsideEligibleRecipientsCannotReceiveVoltages()
    {
        var playing = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid(), null);
        var disconnected = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid(), null);
        var point = new Point3(1, 2, 3);

        var snapshots = SubsystemElectricity.BuildNetworkSnapshots(10, [playing],
            new Dictionary<Point3, float> { [point] = 1f }, _ => [disconnected]);

        Assert.Single(snapshots);
        Assert.Empty(snapshots[playing].SaveData);
        Assert.False(snapshots.ContainsKey(disconnected));
    }
}
