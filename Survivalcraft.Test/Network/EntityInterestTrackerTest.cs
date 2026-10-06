using Game.Network;

namespace Survivalcraft.Test.Network;

public sealed class EntityInterestTrackerTest
{
    private readonly Client _client = new(null, 1, Guid.NewGuid(), Guid.NewGuid(), null);

    [Theory]
    [InlineData(EntityInterestGroup.Creatures)]
    [InlineData(EntityInterestGroup.Pickables)]
    [InlineData(EntityInterestGroup.Projectiles)]
    [InlineData(EntityInterestGroup.MovingBlocks)]
    [InlineData(EntityInterestGroup.Inventories)]
    [InlineData(EntityInterestGroup.BlockEntities)]
    public void ReentryAfterLeavingProducesFreshBaselineTransition(EntityInterestGroup group)
    {
        var tracker = new EntityInterestTracker();
        tracker.Seed(_client, group, [10]);

        var left = tracker.Synchronize(_client, group, new HashSet<int>(), _ => true, 1);

        Assert.Equal([10], left.Left);
        Assert.Empty(tracker.GetObservers(group, 10));

        var reentered = tracker.Synchronize(_client, group, new HashSet<int> { 10 }, _ => true, 1);

        Assert.Equal([10], reentered.Entered);
        Assert.Empty(reentered.Left);
        Assert.Equal([_client], tracker.GetObservers(group, 10));
    }

    [Fact]
    public void ReusedConnectionNumberDoesNotInheritObservedEntities()
    {
        var tracker = new EntityInterestTracker();
        tracker.Seed(_client, EntityInterestGroup.Creatures, [10]);
        var replacement = new Client(null, _client.ID, Guid.NewGuid(), _client.GUID, null);

        tracker.RetainClients([replacement]);
        var changes = tracker.Synchronize(
            replacement, EntityInterestGroup.Creatures, new HashSet<int> { 10 }, _ => true, 1);

        Assert.Equal([10], changes.Entered);
        Assert.Same(replacement, Assert.Single(tracker.GetObservers(EntityInterestGroup.Creatures, 10)));
    }

    [Fact]
    public void RetainingConnectionsRemovesDepartedClientsFromEveryGroup()
    {
        var tracker = new EntityInterestTracker();
        var other = new Client(null, 2, Guid.NewGuid(), Guid.NewGuid(), null);
        foreach (var group in Enum.GetValues<EntityInterestGroup>())
        {
            tracker.Seed(_client, group, [10]);
            tracker.Seed(other, group, [10]);
        }

        tracker.RetainClients([other]);

        foreach (var group in Enum.GetValues<EntityInterestGroup>())
        {
            Assert.Equal([other], tracker.GetObservers(group, 10));
        }

        tracker.RetainClients([]);
        Assert.Empty(tracker.GetObservers(EntityInterestGroup.Creatures, 10));
    }

    [Fact]
    public void ReportsEntityEnteringOnlyOnce()
    {
        var tracker = new EntityInterestTracker();

        var first = tracker.Synchronize(
            _client,
            EntityInterestGroup.Creatures,
            new HashSet<int> { 10 },
            _ => true,
            3);
        var second = tracker.Synchronize(
            _client,
            EntityInterestGroup.Creatures,
            new HashSet<int> { 10 },
            _ => true,
            3);

        Assert.Equal([10], first.Entered);
        Assert.Empty(first.Left);
        Assert.Empty(second.Entered);
        Assert.Empty(second.Left);
    }

    [Fact]
    public void DelaysLeavingUntilThreshold()
    {
        var tracker = new EntityInterestTracker();
        tracker.Seed(_client, EntityInterestGroup.Creatures, [10]);

        for (var i = 0; i < 2; i++)
        {
            var pending = tracker.Synchronize(
                _client,
                EntityInterestGroup.Creatures,
                new HashSet<int>(),
                _ => true,
                3);
            Assert.Empty(pending.Left);
        }

        var left = tracker.Synchronize(
            _client,
            EntityInterestGroup.Creatures,
            new HashSet<int>(),
            _ => true,
            3);

        Assert.Equal([10], left.Left);
    }

    [Fact]
    public void ReenteringDuringHysteresisCancelsLeave()
    {
        var tracker = new EntityInterestTracker();
        tracker.Seed(_client, EntityInterestGroup.Creatures, [10]);
        tracker.Synchronize(
            _client,
            EntityInterestGroup.Creatures,
            new HashSet<int>(),
            _ => true,
            3);

        var current = tracker.Synchronize(
            _client,
            EntityInterestGroup.Creatures,
            new HashSet<int> { 10 },
            _ => true,
            3);

        Assert.Empty(current.Entered);
        Assert.Empty(current.Left);
    }

    [Fact]
    public void RemovedEntityLeavesTrackingWithoutDespawnTransition()
    {
        var tracker = new EntityInterestTracker();
        tracker.Seed(_client, EntityInterestGroup.Creatures, [10]);

        var changes = tracker.Synchronize(
            _client,
            EntityInterestGroup.Creatures,
            new HashSet<int>(),
            _ => false,
            3);

        Assert.Empty(changes.Entered);
        Assert.Empty(changes.Left);
    }

    [Fact]
    public void GroupsAreTrackedIndependently()
    {
        var tracker = new EntityInterestTracker();

        var creatures = tracker.Synchronize(
            _client,
            EntityInterestGroup.Creatures,
            new HashSet<int> { 10 },
            _ => true,
            1);
        var pickables = tracker.Synchronize(
            _client,
            EntityInterestGroup.Pickables,
            new HashSet<int> { 10 },
            _ => true,
            1);

        Assert.Equal([10], creatures.Entered);
        Assert.Equal([10], pickables.Entered);
    }

    [Theory]
    [InlineData(EntityInterestGroup.Creatures)]
    [InlineData(EntityInterestGroup.Pickables)]
    [InlineData(EntityInterestGroup.Projectiles)]
    [InlineData(EntityInterestGroup.MovingBlocks)]
    [InlineData(EntityInterestGroup.Inventories)]
    [InlineData(EntityInterestGroup.BlockEntities)]
    public void DestroyedIdReusedDuringLeaveHysteresisReceivesFreshBaseline(EntityInterestGroup group)
    {
        var tracker = new EntityInterestTracker();
        tracker.Seed(_client, group, [10]);
        tracker.Synchronize(_client, group, new HashSet<int>(), _ => true, 3);
        tracker.Synchronize(_client, group, new HashSet<int>(), _ => true, 3);

        tracker.RemoveEntity(group, 10);

        Assert.Empty(tracker.GetObservers(group, 10));
        var recreated = tracker.Synchronize(_client, group, new HashSet<int> { 10 }, _ => true, 3);
        Assert.Equal([10], recreated.Entered);
        Assert.Empty(recreated.Left);
        Assert.Same(_client, Assert.Single(tracker.GetObservers(group, 10)));

        var firstLeave = tracker.Synchronize(_client, group, new HashSet<int>(), _ => true, 3);
        var secondLeave = tracker.Synchronize(_client, group, new HashSet<int>(), _ => true, 3);
        var finalLeave = tracker.Synchronize(_client, group, new HashSet<int>(), _ => true, 3);

        Assert.Empty(firstLeave.Left);
        Assert.Empty(secondLeave.Left);
        Assert.Equal([10], finalLeave.Left);
    }

    [Theory]
    [InlineData(EntityInterestGroup.Pickables)]
    [InlineData(EntityInterestGroup.BlockEntities)]
    public void RemovingEntityOnlyAffectsSpecifiedGroup(EntityInterestGroup removedGroup)
    {
        var tracker = new EntityInterestTracker();
        tracker.Seed(_client, EntityInterestGroup.Creatures, [10]);
        tracker.Seed(_client, removedGroup, [10]);

        tracker.RemoveEntity(removedGroup, 10);

        Assert.Equal([_client], tracker.GetObservers(EntityInterestGroup.Creatures, 10));
        Assert.Empty(tracker.GetObservers(removedGroup, 10));
    }
}
