using Engine.Core;

using Game.Network;
using Game.Subsystems;

namespace Survivalcraft.Test.Network;

public sealed class ElectricityInterestTest
{
    [Fact]
    public void VoltagesOnlyContainObservedChunksAndDoNotAliasAuthority()
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

        var snapshots = SubsystemElectricity.BuildNetworkVoltages([near, far, empty], voltages,
            chunk => chunk == new Point2(-1, -2) ? [near] : [far]);

        Assert.Equal(3, snapshots.Count);
        Assert.Equal(0.5f, Assert.Single(snapshots[near]).Value);
        Assert.True(snapshots[near].ContainsKey(negativePoint));
        Assert.True(snapshots[far].ContainsKey(farPoint));
        Assert.Empty(snapshots[empty]);
        voltages[negativePoint] = 0f;
        Assert.Equal(0.5f, snapshots[near][negativePoint]);
    }

    [Fact]
    public void ObserversOutsideEligibleRecipientsCannotReceiveVoltages()
    {
        var playing = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid(), null);
        var disconnected = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid(), null);
        var point = new Point3(1, 2, 3);

        var snapshots = SubsystemElectricity.BuildNetworkVoltages([playing],
            new Dictionary<Point3, float> { [point] = 1f }, _ => [disconnected]);

        Assert.Single(snapshots);
        Assert.Empty(snapshots[playing]);
        Assert.False(snapshots.ContainsKey(disconnected));
    }
}
