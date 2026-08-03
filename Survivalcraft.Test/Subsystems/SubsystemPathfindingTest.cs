using Engine.Core;

using Game;
using Game.Subsystems;

namespace Survivalcraft.Test.Subsystems;

public sealed class SubsystemPathfindingTest
{
    [Theory]
    [InlineData(1, true, 1)]
    [InlineData(4, true, 1)]
    [InlineData(6, true, 2)]
    [InlineData(8, true, 2)]
    [InlineData(12, true, 3)]
    [InlineData(16, true, 4)]
    [InlineData(16, false, 2)]
    public void WorkerPolicyReservesProcessorsForOtherSubsystems(
        int processorCount,
        bool isServer,
        int expected)
    {
        Assert.Equal(expected, SubsystemPathfinding.CalculateWorkerCount(processorCount, isServer));
    }

    [Fact]
    public void BoundedQueueRejectsOverflowAndDisposeCompletesPendingRequests()
    {
        var subsystem = new SubsystemPathfinding();
        var accepted = new List<PathfindingResult>();
        for (var i = 0; i < SubsystemPathfinding.maxPendingRequests; i++)
        {
            var result = new PathfindingResult();
            accepted.Add(result);
            subsystem.QueuePathSearch(Vector3.Zero, Vector3.One, 1f, Vector3.One, false, 100, result);
            Assert.True(result.IsInProgress);
            Assert.False(result.IsCompleted);
        }

        var rejected = new PathfindingResult();
        subsystem.QueuePathSearch(Vector3.Zero, Vector3.One, 1f, Vector3.One, false, 100, rejected);

        Assert.Equal(SubsystemPathfinding.maxPendingRequests, subsystem.PendingRequestCount);
        Assert.False(rejected.IsInProgress);
        Assert.True(rejected.IsCompleted);

        subsystem.Dispose();

        Assert.Equal(0, subsystem.PendingRequestCount);
        Assert.All(accepted, result =>
        {
            Assert.False(result.IsInProgress);
            Assert.True(result.IsCompleted);
        });
    }
}
