using Engine.Core;

using Game.Network;

namespace Survivalcraft.Test.Network;

public sealed class ElectricityReplicationTrackerTest
{
    [Fact]
    public void BaselineThenChangesAndRemovalsPreserveAuthority()
    {
        var tracker = new ElectricityReplicationTracker();
        var client = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid(), null);
        var point = new Point3(1, 2, 3);
        var values = new Dictionary<Point3, float> { [point] = 0.5f };

        var baseline = tracker.Capture(client, values);
        Assert.True(baseline.IsBaseline);
        Assert.Equal(0.5f, baseline.Voltages[point]);
        Assert.Empty(tracker.Capture(client, values).Voltages);
        values[point] = 1f;
        var delta = tracker.Capture(client, values);
        Assert.False(delta.IsBaseline);
        Assert.Equal(1f, delta.Voltages[point]);
        Assert.Equal(0.5f, baseline.Voltages[point]);
        values.Clear();
        Assert.Equal([point], tracker.Capture(client, values).Removed);
        values[point] = 1f;
        Assert.Equal(1f, tracker.Capture(client, values).Voltages[point]);
    }

    [Fact]
    public void ReconnectedClientAndRemovedConnectionRequireFreshBaselines()
    {
        var tracker = new ElectricityReplicationTracker();
        var first = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid(), null);
        var reconnected = new Client(null, 1, Guid.NewGuid(), first.GUID, null);
        tracker.Capture(first, new Dictionary<Point3, float>());

        Assert.True(tracker.Capture(reconnected, new Dictionary<Point3, float>()).IsBaseline);
        tracker.RetainClients([reconnected]);
        Assert.True(tracker.Capture(first, new Dictionary<Point3, float>()).IsBaseline);
    }
}
