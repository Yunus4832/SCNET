using Engine.Core.Mathematics;

using Game.Terrains;

namespace Survivalcraft.Core.Test.Terrains;

public sealed class DeferredTerrainChangesTest
{
    [Fact]
    public void CapacityRejectsNewIntentsWithoutSynchronousWorkAndStillCoalescesExistingCells()
    {
        var fixture = new Fixture(2);
        var first = fixture.Cell(0);
        var second = fixture.Cell(1);
        var third = fixture.Cell(2);
        Assert.True(fixture.Queue.TryEnqueue(first, 2));
        Assert.True(fixture.Queue.TryEnqueue(second, 2));
        Assert.False(fixture.Queue.TryEnqueue(third, 2));
        fixture.Cells[first] = new DeferredTerrainChanges.Cell(2, 1);
        Assert.True(fixture.Queue.TryEnqueue(first, 3));
        Assert.Empty(fixture.Applied);
        fixture.Queue.StartBatch();
        Assert.Equal(2, fixture.Queue.Drain(32, TimeSpan.FromSeconds(1)));
        Assert.Equal(new[] { (first, 3), (second, 2) }, fixture.Applied);
    }

    [Fact]
    public void RelatedIntentsAreRejectedTogetherWithoutUpdatingAnAlreadyQueuedCoordinate()
    {
        var fixture = new Fixture(3);
        var first = fixture.Cell(0);
        var second = fixture.Cell(1);
        var third = fixture.Cell(2);
        var fourth = fixture.Cell(3);
        fixture.Queue.TryEnqueue(first, 2);
        fixture.Queue.TryEnqueue(second, 2);
        Assert.False(fixture.Queue.TryEnqueue([(first, 20), (third, 2), (fourth, 2)]));
        fixture.Cells.Remove(fourth);
        Assert.False(fixture.Queue.TryEnqueue([(first, 20), (fourth, 2)]));
        Assert.Equal(2, fixture.Queue.Count);
        fixture.Queue.StartBatch();
        fixture.Queue.Drain(32, TimeSpan.FromSeconds(1));
        Assert.Equal(new[] { (first, 2), (second, 2) }, fixture.Applied);
    }

    [Fact]
    public void TheNextAtomicChangeWaitsForTheNextFrameWhenElapsedBudgetIsSpent()
    {
        var fixture = new Fixture();
        foreach (var index in Enumerable.Range(0, 3)) { fixture.Queue.TryEnqueue(fixture.Cell(index), 2); }

        fixture.OnApply = (_, _) => fixture.Time.Milliseconds += 10;
        fixture.Queue.StartBatch();
        Assert.Equal(1, fixture.Queue.Drain(32, TimeSpan.FromMilliseconds(2)));
        Assert.Equal(2, fixture.Queue.Count);
        Assert.True(fixture.Queue.HasActiveBatch);
        Assert.Equal(1, fixture.Queue.Drain(32, TimeSpan.FromMilliseconds(2)));
        Assert.Equal(1, fixture.Queue.Drain(32, TimeSpan.FromMilliseconds(2)));
        Assert.False(fixture.Queue.HasActiveBatch);
    }

    [Fact]
    public void ReentrantNewIntentsBelongToALaterBatchAndCannotRecursivelyDrain()
    {
        var fixture = new Fixture();
        var point = fixture.Cell(0);
        fixture.Queue.TryEnqueue(point, 2);
        fixture.OnApply = (p, value) =>
        {
            Assert.True(fixture.Queue.TryEnqueue(p, value + 1));
            fixture.Queue.StartBatch();
            Assert.Equal(0, fixture.Queue.Drain(32, TimeSpan.FromSeconds(1)));
        };
        fixture.Queue.StartBatch();
        Assert.Equal(1, fixture.Queue.Drain(32, TimeSpan.FromSeconds(1)));
        Assert.Equal(1, fixture.Queue.Count);
        Assert.False(fixture.Queue.HasActiveBatch);
        fixture.OnApply = null;
        fixture.Queue.StartBatch();
        Assert.Equal(1, fixture.Queue.Drain(32, TimeSpan.FromSeconds(1)));
        Assert.Equal(3, fixture.Cells[point].Value);
    }

    [Fact]
    public void ConditionsIgnoreLightButRejectChangedContentsMissingChunksAndNewAllocationsWithinItemBudget()
    {
        var fixture = new Fixture();
        var points = Enumerable.Range(0, 4).Select(fixture.Cell).ToArray();
        foreach (var point in points) { fixture.Queue.TryEnqueue(point, 2); }

        fixture.Cells[points[0]] = new DeferredTerrainChanges.Cell(Terrain.ReplaceLight(1, 14), 1);
        fixture.Cells[points[1]] = new DeferredTerrainChanges.Cell(3, 1);
        fixture.Cells[points[2]] = new DeferredTerrainChanges.Cell(1, 2);
        fixture.Cells.Remove(points[3]);
        fixture.Queue.StartBatch();
        Assert.Equal(1, fixture.Queue.Drain(2, TimeSpan.FromSeconds(1)));
        Assert.Equal(2, fixture.Queue.Count);
        Assert.Equal(0, fixture.Queue.Drain(2, TimeSpan.FromSeconds(1)));
        Assert.Equal((points[0], 2), Assert.Single(fixture.Applied));
        Assert.False(fixture.Queue.HasActiveBatch);
    }

    [Fact]
    public void CallbackFailureDoesNotReplayAnAlreadyConsumedIntent()
    {
        var fixture = new Fixture();
        fixture.Queue.TryEnqueue(fixture.Cell(0), 2);
        fixture.Queue.TryEnqueue(fixture.Cell(1), 2);
        fixture.OnApply = (_, _) => throw new InvalidOperationException("test callback");
        fixture.Queue.StartBatch();
        Assert.Throws<InvalidOperationException>(() => fixture.Queue.Drain(32, TimeSpan.FromSeconds(1)));
        fixture.OnApply = null;
        Assert.Equal(1, fixture.Queue.Drain(32, TimeSpan.FromSeconds(1)));
        Assert.Equal(2, fixture.Applied.Count);
        Assert.Equal(0, fixture.Queue.Count);
    }

    [Fact]
    public void ClearingDuringACallbackStopsTheBatchAndDropsOnlyUncommittedIntents()
    {
        var fixture = new Fixture();
        fixture.Queue.TryEnqueue(fixture.Cell(0), 2);
        fixture.Queue.TryEnqueue(fixture.Cell(1), 2);
        fixture.OnApply = (_, _) => fixture.Queue.Clear();
        fixture.Queue.StartBatch();
        Assert.Equal(1, fixture.Queue.Drain(32, TimeSpan.FromSeconds(1)));
        Assert.Single(fixture.Applied);
        Assert.Equal(0, fixture.Queue.Count);
        Assert.False(fixture.Queue.HasActiveBatch);
    }

    private sealed class Fixture
    {
        public Dictionary<Point3, DeferredTerrainChanges.Cell> Cells { get; } = [];

        public List<(Point3, int)> Applied { get; } = [];

        public Clock Time { get; } = new();

        public DeferredTerrainChanges Queue { get; }

        public Action<Point3, int>? OnApply { get; set; }

        public Fixture(int capacity = 10000)
        {
            Queue = new DeferredTerrainChanges(
                point => Cells.TryGetValue(point, out var cell) ? cell : null,
                (point, value) =>
                {
                    Applied.Add((point, value));
                    Cells[point] = new DeferredTerrainChanges.Cell(value, Cells[point].AllocationGeneration);
                    OnApply?.Invoke(point, value);
                },
                capacity,
                Time
            );
        }

        public Point3 Cell(int index)
        {
            var point = new Point3(index, 1, 0);
            Cells[point] = new DeferredTerrainChanges.Cell(1, 1);
            return point;
        }
    }

    private sealed class Clock : TimeProvider
    {
        public long Milliseconds { get; set; }

        public override long TimestampFrequency => 1000;

        public override long GetTimestamp() => Milliseconds;
    }
}
