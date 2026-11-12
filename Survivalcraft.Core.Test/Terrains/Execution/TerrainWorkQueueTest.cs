using Game.Terrains;
using Game.Terrains.Execution;

namespace Survivalcraft.Core.Test.Terrains.Execution;

public sealed class TerrainWorkQueueTest
{
    [Fact]
    public void TemporarilyIneligibleWorkRetainsItsReadySlotWhileOtherStagesRun()
    {
        using var terrain = new Terrain();
        var queue = new TerrainWorkQueue();
        var light = new TerrainWorkKey(terrain.AllocateChunk(0, 0), TerrainWorkStage.Propagation);
        var mesh = new TerrainWorkKey(terrain.AllocateChunk(1, 0), TerrainWorkStage.Derived);
        queue.Enqueue(light, 1, TerrainWorkPriority.FirstDisplay, 1);
        queue.Enqueue(mesh, 1, TerrainWorkPriority.Maintenance, 1000);
        var selected = queue.Take(static key => key.Stage != TerrainWorkStage.Propagation)!;
        Assert.Equal(mesh, selected.Key);
        queue.Finish(selected, true);
        Assert.Null(queue.Take(static key => key.Stage != TerrainWorkStage.Propagation));
        Assert.Equal((1, 1, 0), queue.Counts);
        Assert.Equal(light, queue.Take()!.Key);
    }

    [Fact]
    public void AsyncServiceCostUsesTheAdmissionAgingDecision()
    {
        using var terrain = new Terrain();
        var time = new QueueTime();
        var queue = new TerrainWorkQueue(time);
        var far = new TerrainWorkKey(terrain.AllocateChunk(2, 0), TerrainWorkStage.Propagation);
        queue.Enqueue(far, 1, TerrainWorkPriority.Maintenance, 1000);
        time.Milliseconds = 600;
        var near = new TerrainWorkKey(terrain.AllocateChunk(0, 0), TerrainWorkStage.Propagation);
        queue.Enqueue(near, 1, TerrainWorkPriority.Maintenance, 1);
        var first = queue.Take()!;
        Assert.Equal(far, first.Key);
        Assert.True(first.WasAgingSelection);
        queue.Wait(first, []);
        var second = queue.Take()!;
        Assert.Equal(near, second.Key);
        Assert.False(second.WasAgingSelection);
        queue.RecordCost(first.Priority, TimeSpan.FromMilliseconds(5), first.WasAgingSelection);
        queue.Finish(first, true);
        Assert.False(queue.IsRegistered(first));
        Assert.True(queue.IsRegistered(second));
    }

    [Theory]
    [InlineData(TerrainWorkPriority.FirstDisplay)]
    [InlineData(TerrainWorkPriority.Maintenance)]
    public void NearViewDistanceReachesTheEndOfAnAlreadyBlockedDependencyChain(TerrainWorkPriority priority)
    {
        using var terrain = new Terrain();
        var queue = new TerrainWorkQueue();
        var target = new TerrainWorkKey(terrain.AllocateChunk(0, 0), TerrainWorkStage.Derived);
        var neighbor = new TerrainWorkKey(terrain.AllocateChunk(1, 0), TerrainWorkStage.Propagation);
        var prerequisite = new TerrainWorkKey(terrain.AllocateChunk(2, 0), TerrainWorkStage.LightSeed);
        queue.Enqueue(neighbor, 1, priority, 100);
        queue.Wait(queue.Take()!, [prerequisite]);
        queue.Enqueue(prerequisite, 1, priority, 1000);
        queue.Enqueue(target, 1, priority, 1);
        queue.Wait(queue.Take()!, [neighbor]);
        var unrelated = new TerrainWorkKey(terrain.AllocateChunk(3, 0), TerrainWorkStage.Heights);
        queue.Enqueue(unrelated, 1, priority, 10);

        Assert.Equal(prerequisite, queue.Take()!.Key);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MovingOrCancellingTheNearWaiterWithdrawsItsDistanceFromTheEntireChain(bool move)
    {
        using var terrain = new Terrain();
        var queue = new TerrainWorkQueue();
        var target = new TerrainWorkKey(terrain.AllocateChunk(0, 0), TerrainWorkStage.Derived);
        var neighbor = new TerrainWorkKey(terrain.AllocateChunk(1, 0), TerrainWorkStage.Propagation);
        var prerequisite = new TerrainWorkKey(terrain.AllocateChunk(2, 0), TerrainWorkStage.LightSeed);
        queue.Enqueue(neighbor, 1, TerrainWorkPriority.FirstDisplay, 100);
        queue.Wait(queue.Take()!, [prerequisite]);
        queue.Enqueue(prerequisite, 1, TerrainWorkPriority.FirstDisplay, 1000);
        queue.Enqueue(target, 1, TerrainWorkPriority.FirstDisplay, 1);
        queue.Wait(queue.Take()!, [neighbor]);
        var running = Assert.IsType<TerrainWorkQueue.Item>(queue.Take());
        Assert.Equal(1, running.Distance);
        queue.Finish(running, false);
        if (move) { queue.Enqueue(target, 1, TerrainWorkPriority.FirstDisplay, 2000); }
        else { queue.Cancel(target.Chunk); }

        var unrelated = new TerrainWorkKey(terrain.AllocateChunk(3, 0), TerrainWorkStage.Heights);
        queue.Enqueue(unrelated, 1, TerrainWorkPriority.FirstDisplay, 10);
        Assert.Equal(unrelated, queue.Take()!.Key);
    }

    [Fact]
    public void AnEarlierStageOfTheRequiredNeighborInheritsTheNearViewsDistance()
    {
        using var terrain = new Terrain();
        var queue = new TerrainWorkQueue();
        var target = new TerrainWorkKey(terrain.AllocateChunk(0, 0), TerrainWorkStage.Derived);
        var neighbor = terrain.AllocateChunk(1, 0);
        queue.Enqueue(target, 1, TerrainWorkPriority.FirstDisplay, 1);
        queue.Wait(queue.Take()!, [new TerrainWorkKey(neighbor, TerrainWorkStage.Propagation)]);
        var sunlight = new TerrainWorkKey(neighbor, TerrainWorkStage.LightSeed);
        queue.Enqueue(sunlight, 1, TerrainWorkPriority.FirstDisplay, 100);
        queue.Enqueue(
            new TerrainWorkKey(terrain.AllocateChunk(2, 0), TerrainWorkStage.Heights),
            1,
            TerrainWorkPriority.FirstDisplay,
            10
        );
        Assert.Equal(sunlight, queue.Take()!.Key);
    }

    [Fact]
    public void NeedingNeighborLightDoesNotPromoteTheNeighborsUnrelatedMeshDependencies()
    {
        using var terrain = new Terrain();
        var queue = new TerrainWorkQueue();
        var nearMesh = new TerrainWorkKey(terrain.AllocateChunk(0, 0), TerrainWorkStage.Derived);
        var neighbor = terrain.AllocateChunk(1, 0);
        var further = terrain.AllocateChunk(2, 0);
        queue.Enqueue(nearMesh, 1, TerrainWorkPriority.FirstDisplay, 1);
        queue.Wait(queue.Take()!, [new TerrainWorkKey(neighbor, TerrainWorkStage.Propagation)]);
        queue.Enqueue(new TerrainWorkKey(neighbor, TerrainWorkStage.Derived), 1, TerrainWorkPriority.FirstDisplay, 100);
        queue.Wait(queue.Take()!, [new TerrainWorkKey(further, TerrainWorkStage.Propagation)]);
        queue.Enqueue(
            new TerrainWorkKey(further, TerrainWorkStage.LightSeed),
            1,
            TerrainWorkPriority.FirstDisplay,
            1000
        );
        var unrelated = new TerrainWorkKey(terrain.AllocateChunk(3, 0), TerrainWorkStage.Heights);
        queue.Enqueue(unrelated, 1, TerrainWorkPriority.FirstDisplay, 10);
        Assert.Equal(unrelated, queue.Take()!.Key);
    }

    [Fact]
    public void UpdatingAnEarlierStageAlsoMovesItsOlderBlockedStage()
    {
        using var terrain = new Terrain();
        var queue = new TerrainWorkQueue();
        var near = terrain.AllocateChunk(0, 0);
        var neighbor = terrain.AllocateChunk(1, 0);
        queue.Enqueue(new TerrainWorkKey(near, TerrainWorkStage.Derived), 1, TerrainWorkPriority.FirstDisplay, 1);
        queue.Wait(queue.Take()!, [new TerrainWorkKey(neighbor, TerrainWorkStage.Propagation)]);
        queue.Enqueue(
            new TerrainWorkKey(neighbor, TerrainWorkStage.LightSeed),
            1,
            TerrainWorkPriority.FirstDisplay,
            100
        );
        queue.Enqueue(new TerrainWorkKey(near, TerrainWorkStage.LightSeed), 1, TerrainWorkPriority.FirstDisplay, 2000);
        var unrelated = new TerrainWorkKey(terrain.AllocateChunk(2, 0), TerrainWorkStage.Heights);
        queue.Enqueue(unrelated, 1, TerrainWorkPriority.FirstDisplay, 10);
        Assert.Equal(unrelated, queue.Take()!.Key);
    }

    [Fact]
    public void DependencyPriorityTracksLiveWaitersAcrossPrerequisiteStagesAndEndsWithTheirWait()
    {
        using var terrain = new Terrain();
        var queue = new TerrainWorkQueue();
        var target = terrain.AllocateChunk(0, 0);
        var neighbor = terrain.AllocateChunk(1, 0);
        var finalPrerequisite = new TerrainWorkKey(neighbor, TerrainWorkStage.Propagation);
        var mesh = new TerrainWorkKey(target, TerrainWorkStage.Derived);
        queue.Enqueue(mesh, 1, TerrainWorkPriority.FirstDisplay, 1);
        queue.Wait(queue.Take()!, [finalPrerequisite]);

        Assert.Equal(TerrainWorkPriority.FirstDisplay, queue.GetDependentPriority(neighbor));
        queue.Enqueue(
            new TerrainWorkKey(neighbor, TerrainWorkStage.Heights),
            1,
            TerrainWorkPriority.FirstDisplay,
            2
        );
        queue.Finish(queue.Take()!, true);
        Assert.Equal(TerrainWorkPriority.FirstDisplay, queue.GetDependentPriority(neighbor));

        queue.Enqueue(mesh, 1, TerrainWorkPriority.Critical, 1);
        Assert.Equal(TerrainWorkPriority.Critical, queue.GetDependentPriority(neighbor));
        queue.Signal(finalPrerequisite);
        Assert.Null(queue.GetDependentPriority(neighbor));
        Assert.Equal(mesh, queue.Take()!.Key);
    }

    [Fact]
    public void NearerInputWorkPrecedesDistantFirstGeometryWithoutStarvingTheOlderGeometry()
    {
        using var terrain = new Terrain();
        var time = new QueueTime();
        var queue = new TerrainWorkQueue(time);
        var geometry = new TerrainWorkKey(terrain.AllocateChunk(4, 0), TerrainWorkStage.Derived);
        var input = new TerrainWorkKey(terrain.AllocateChunk(0, 0), TerrainWorkStage.LightSeed);
        queue.Enqueue(input, 1, TerrainWorkPriority.FirstDisplay, 1);
        queue.Enqueue(geometry, 1, TerrainWorkPriority.FirstDisplay, 10000);
        var first = queue.Take()!;
        Assert.Equal(input, first.Key);
        queue.Finish(first, true);
        time.Milliseconds = 1;
        queue.Enqueue(input, 2, TerrainWorkPriority.FirstDisplay, 1);
        time.Milliseconds = 600;
        Assert.Equal(geometry, queue.Take()!.Key);
    }

    [Theory]
    [InlineData(TerrainWorkPriority.FirstDisplay)]
    [InlineData(TerrainWorkPriority.Maintenance)]
    public void AnAgedContentBatchCannotStallTheNearChunkStageChain(TerrainWorkPriority priority)
    {
        using var terrain = new Terrain();
        var time = new QueueTime();
        var queue = new TerrainWorkQueue(time);
        for (var i = 0; i < 64; i++)
        {
            queue.Enqueue(
                new TerrainWorkKey(terrain.AllocateChunk(i + 10, 0), TerrainWorkStage.Content),
                1,
                priority,
                10000 + i
            );
        }

        time.Milliseconds = 600;
        var near = terrain.AllocateChunk(0, 0);
        TerrainWorkStage[] stages =
        [
            TerrainWorkStage.Heights, TerrainWorkStage.LightSeed, TerrainWorkStage.Propagation, TerrainWorkStage.Derived
        ];
        var agedTurns = 0;
        foreach (var stage in stages)
        {
            var target = new TerrainWorkKey(near, stage);
            queue.Enqueue(target, 1, priority, 1);
            var advanced = false;
            for (var turn = 0; turn < 2; turn++)
            {
                var item = Assert.IsType<TerrainWorkQueue.Item>(queue.Take());
                advanced = item.Key == target;
                if (!advanced)
                {
                    Assert.Equal(TerrainWorkStage.Content, item.Key.Stage);
                    agedTurns++;
                }

                // A content task may need several slices; it remains old between slices.
                queue.Finish(item, advanced);
                queue.RecordCost(item.Priority, TimeSpan.FromMilliseconds(5));
                time.Milliseconds += 5;
                if (advanced) { break; }
            }

            Assert.True(advanced, $"Near {stage} did not advance while distant contents were runnable.");
        }

        Assert.True(agedTurns > 0, "Distance preference must not starve old distant work either.");
        Assert.Equal(64, queue.Counts.Total);
    }

    [Theory]
    [InlineData(TerrainWorkPriority.FirstDisplay)]
    [InlineData(TerrainWorkPriority.Maintenance)]
    public void ExpensiveAgingWorkLetsTheNearChunkFinishItsCheaperStages(TerrainWorkPriority priority)
    {
        using var terrain = new Terrain();
        var time = new QueueTime();
        var queue = new TerrainWorkQueue(time);
        var far = new TerrainWorkKey(terrain.AllocateChunk(8, 0), TerrainWorkStage.Content);
        var near = terrain.AllocateChunk(0, 0);
        queue.Enqueue(far, 1, priority, 10000);
        time.Milliseconds = 600;
        queue.Enqueue(new TerrainWorkKey(near, TerrainWorkStage.Heights), 1, priority, 1);
        var aged = Assert.IsType<TerrainWorkQueue.Item>(queue.Take());
        Assert.Equal(far, aged.Key);
        queue.Finish(aged, false);
        queue.RecordCost(priority, TimeSpan.FromMilliseconds(20));
        time.Milliseconds += 20;

        TerrainWorkStage[] stages =
        [
            TerrainWorkStage.Heights, TerrainWorkStage.LightSeed, TerrainWorkStage.Propagation, TerrainWorkStage.Derived
        ];
        foreach (var stage in stages)
        {
            var target = new TerrainWorkKey(near, stage);
            queue.Enqueue(target, 1, priority, 1);
            var selected = Assert.IsType<TerrainWorkQueue.Item>(queue.Take());
            Assert.Equal(target, selected.Key);
            queue.Finish(selected, true);
            queue.RecordCost(priority, TimeSpan.FromMilliseconds(1));
            time.Milliseconds++;
        }

        // A persistent near workload must still give the distant old task another turn.
        var returnedToFar = false;
        for (var revision = 2; revision <= 12; revision++)
        {
            queue.Enqueue(new TerrainWorkKey(near, TerrainWorkStage.Heights), revision, priority, 1);
            var selected = Assert.IsType<TerrainWorkQueue.Item>(queue.Take());
            if (selected.Key == far)
            {
                returnedToFar = true;
                break;
            }

            queue.Finish(selected, true);
            queue.RecordCost(priority, TimeSpan.FromMilliseconds(10));
            time.Milliseconds += 10;
        }

        Assert.True(returnedToFar, "Elapsed-time preference must still reserve service for old distant work.");
    }

    [Fact]
    public void MaintenanceRemainsRunnableWhileFirstDisplayRepaysAnExpensiveAgingTurn()
    {
        using var terrain = new Terrain();
        var time = new QueueTime();
        var queue = new TerrainWorkQueue(time);
        var far = new TerrainWorkKey(terrain.AllocateChunk(8, 0), TerrainWorkStage.Content);
        queue.Enqueue(far, 1, TerrainWorkPriority.FirstDisplay, 10000);
        time.Milliseconds = 600;
        queue.Enqueue(
            new TerrainWorkKey(terrain.AllocateChunk(0, 0), TerrainWorkStage.Heights),
            1,
            TerrainWorkPriority.FirstDisplay,
            1
        );
        var maintenance = new TerrainWorkKey(terrain.AllocateChunk(1, 0), TerrainWorkStage.Derived);
        queue.Enqueue(maintenance, 1, TerrainWorkPriority.Maintenance, 100);
        var selected = Assert.IsType<TerrainWorkQueue.Item>(queue.Take());
        Assert.Equal(far, selected.Key);
        queue.Finish(selected, false);
        queue.RecordCost(selected.Priority, TimeSpan.FromMilliseconds(20));
        Assert.Equal(maintenance, queue.Take()!.Key);
    }

    [Fact]
    public void DroppingAStaleReadyKeyDefensivelyWakesItsDependentsWithoutAnExplicitCancel()
    {
        using var terrain = new Terrain();
        var queue = new TerrainWorkQueue();
        var chunk = terrain.AllocateChunk(0, 0);
        var neighbor = terrain.AllocateChunk(1, 0);
        var mesh = new TerrainWorkKey(chunk, TerrainWorkStage.Derived);
        var dependency = new TerrainWorkKey(neighbor, TerrainWorkStage.LightSeed);
        queue.Enqueue(mesh, 1, TerrainWorkPriority.FirstDisplay, 1);
        var waiting = Assert.IsType<TerrainWorkQueue.Item>(queue.Take());
        queue.Wait(waiting, [dependency]);
        queue.Enqueue(dependency, 1, TerrainWorkPriority.FirstDisplay, 2);
        terrain.RenewChunkAllocation(neighbor);

        Assert.Same(waiting, queue.Take());
        queue.Finish(waiting, true);
        Assert.Equal((0, 0, 0), queue.Counts);
    }

    [Fact]
    public void SameRevisionPriorityAndDistanceChangesDoNotDiscardRegisteredDependencyWaits()
    {
        using var terrain = new Terrain();
        var queue = new TerrainWorkQueue();
        var target = new TerrainWorkKey(terrain.AllocateChunk(0, 0), TerrainWorkStage.Derived);
        var dependency = new TerrainWorkKey(terrain.AllocateChunk(1, 0), TerrainWorkStage.Propagation);
        queue.Enqueue(target, 4, TerrainWorkPriority.Maintenance, 1000);
        var waiting = Assert.IsType<TerrainWorkQueue.Item>(queue.Take());
        queue.Wait(waiting, [dependency]);

        for (var distance = 9; distance >= 0; distance--)
        {
            queue.Enqueue(target, 4, TerrainWorkPriority.Critical, distance);
            Assert.Null(queue.Take());
            Assert.Equal((1, 0, 1), queue.Counts);
        }

        queue.Signal(dependency);
        var resumed = Assert.IsType<TerrainWorkQueue.Item>(queue.Take());
        Assert.Same(waiting, resumed);
        Assert.Equal(TerrainWorkPriority.Critical, resumed.Priority);
        queue.Finish(resumed, true);
        Assert.Equal((0, 0, 0), queue.Counts);
    }

    [Fact]
    public void ANewInputRevisionCanReevaluateAndDetachObsoleteDependencyWaits()
    {
        using var terrain = new Terrain();
        var queue = new TerrainWorkQueue();
        var chunk = terrain.AllocateChunk(0, 0);
        var neighbor = terrain.AllocateChunk(1, 0);
        var target = new TerrainWorkKey(chunk, TerrainWorkStage.Derived);
        var dependency = new TerrainWorkKey(neighbor, TerrainWorkStage.Propagation);
        queue.Enqueue(target, 4, TerrainWorkPriority.FirstDisplay, 10);
        var waiting = Assert.IsType<TerrainWorkQueue.Item>(queue.Take());
        queue.Wait(waiting, [dependency]);
        queue.Enqueue(target, 5, TerrainWorkPriority.FirstDisplay, 10);

        var updated = Assert.IsType<TerrainWorkQueue.Item>(queue.Take());
        Assert.Same(waiting, updated);
        Assert.Equal(5, updated.Revision);
        Assert.False(queue.HasDependents(neighbor));
        queue.Finish(updated, true);
        queue.Signal(dependency);
        Assert.Null(queue.Take());
        Assert.Equal((0, 0, 0), queue.Counts);
    }

    [Fact]
    public void RepeatedInvalidationCoalescesAndRejectsAnOlderInFlightCompletion()
    {
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        var queue = new TerrainWorkQueue();
        var key = new TerrainWorkKey(chunk, TerrainWorkStage.Heights);
        queue.Enqueue(key, 1, TerrainWorkPriority.FirstDisplay, 1);
        var first = Assert.IsType<TerrainWorkQueue.Item>(queue.Take());
        for (var i = 2; i < 100; i++) { queue.Enqueue(key, i, TerrainWorkPriority.FirstDisplay, 1); }

        Assert.Equal(1, queue.Counts.Total);
        queue.Finish(first, true);
        var retry = Assert.IsType<TerrainWorkQueue.Item>(queue.Take());
        Assert.Equal(99, retry.Revision);
        queue.Finish(retry, true);
        Assert.Equal(0, queue.Counts.Total);
    }

    [Fact]
    public void BlockedTasksDoNotConsumeExecutionAndWakeOnlyAfterAllPrerequisites()
    {
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        var neighbor = terrain.AllocateChunk(1, 0);
        var queue = new TerrainWorkQueue();
        var mesh = new TerrainWorkKey(chunk, TerrainWorkStage.Derived);
        var first = new TerrainWorkKey(chunk, TerrainWorkStage.Propagation);
        var second = new TerrainWorkKey(neighbor, TerrainWorkStage.Propagation);
        queue.Enqueue(mesh, 1, TerrainWorkPriority.FirstDisplay, 1);
        var waiting = Assert.IsType<TerrainWorkQueue.Item>(queue.Take());
        queue.Wait(waiting, [first, second]);
        Assert.Null(queue.Take());
        queue.Signal(first);
        Assert.Null(queue.Take());
        queue.Signal(second);
        Assert.Same(waiting, queue.Take());
        queue.Finish(waiting, true);
        Assert.Equal((0, 0, 0), queue.Counts);
    }

    [Fact]
    public void RetiredAllocationsCannotExecuteOrLeaveDependentsPermanentlyWaiting()
    {
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        var neighbor = terrain.AllocateChunk(1, 0);
        var queue = new TerrainWorkQueue();
        queue.Enqueue(new TerrainWorkKey(chunk, TerrainWorkStage.Derived), 1, TerrainWorkPriority.FirstDisplay, 1);
        var mesh = Assert.IsType<TerrainWorkQueue.Item>(queue.Take());
        var dependency = new TerrainWorkKey(neighbor, TerrainWorkStage.LightSeed);
        queue.Wait(mesh, [dependency]);
        queue.Enqueue(dependency, 1, TerrainWorkPriority.FirstDisplay, 2);
        terrain.FreeChunk(neighbor);
        queue.Cancel(neighbor);
        Assert.Same(mesh, queue.Take());
        queue.Finish(mesh, true);
        Assert.Equal(0, queue.Counts.Total);
        queue.Enqueue(new TerrainWorkKey(chunk, TerrainWorkStage.Heights), 1, TerrainWorkPriority.Maintenance, 1);
        terrain.RenewChunkAllocation(chunk);
        Assert.Null(queue.Take());
        Assert.Equal(0, queue.Counts.Total);
    }

    [Fact]
    public void ElapsedServiceReservesMaintenanceEvenWithAnUnlimitedInitialBacklog()
    {
        using var terrain = new Terrain();
        var queue = new TerrainWorkQueue();
        var first = terrain.AllocateChunk(0, 0);
        var maintained = terrain.AllocateChunk(1, 0);
        var initial = new TerrainWorkKey(first, TerrainWorkStage.Derived);
        var maintenance = new TerrainWorkKey(maintained, TerrainWorkStage.Derived);
        queue.Enqueue(initial, 1, TerrainWorkPriority.FirstDisplay, 1);
        queue.Enqueue(maintenance, 1, TerrainWorkPriority.Maintenance, 1000);
        var selected = Assert.IsType<TerrainWorkQueue.Item>(queue.Take());
        Assert.Equal(initial, selected.Key);
        queue.RecordCost(selected.Priority, TimeSpan.FromMilliseconds(20));
        queue.Finish(selected, false);
        Assert.Equal(maintenance, queue.Take()!.Key);
    }

    [Fact]
    public void AgePreventsNearerArrivalsFromStarvingOldWorkWithinItsServiceClass()
    {
        using var terrain = new Terrain();
        var time = new QueueTime();
        var queue = new TerrainWorkQueue(time);
        var far = new TerrainWorkKey(terrain.AllocateChunk(2, 0), TerrainWorkStage.Derived);
        queue.Enqueue(far, 1, TerrainWorkPriority.Maintenance, 1000);
        time.Milliseconds = 600;
        var near = new TerrainWorkKey(terrain.AllocateChunk(0, 0), TerrainWorkStage.Derived);
        queue.Enqueue(near, 1, TerrainWorkPriority.Maintenance, 1);
        Assert.Equal(far, queue.Take()!.Key);
    }

    [Fact]
    public void CycleRegistrationFailsBeforeDamagingExistingWaits()
    {
        using var terrain = new Terrain();
        var queue = new TerrainWorkQueue();
        var a = new TerrainWorkKey(terrain.AllocateChunk(0, 0), TerrainWorkStage.Propagation);
        var b = new TerrainWorkKey(terrain.AllocateChunk(1, 0), TerrainWorkStage.Propagation);
        queue.Enqueue(a, 1, TerrainWorkPriority.Maintenance, 1);
        queue.Enqueue(b, 1, TerrainWorkPriority.Maintenance, 2);
        var first = queue.Take()!;
        queue.Wait(first, [b]);
        var second = queue.Take()!;
        Assert.Throws<InvalidOperationException>(() => queue.Wait(second, [a]));
        queue.Finish(second, true);
        Assert.Same(first, queue.Take());
    }

    private sealed class QueueTime : TimeProvider
    {
        public long Milliseconds { get; set; }

        public override long TimestampFrequency => 1000;

        public override long GetTimestamp() => Milliseconds;
    }
}
