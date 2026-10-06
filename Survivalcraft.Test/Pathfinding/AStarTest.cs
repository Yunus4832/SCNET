using Engine.Core;

using Game;

namespace Survivalcraft.Test.Pathfinding;

public sealed class AStarTest
{
    [Theory]
    [InlineData(10, 14)]
    [InlineData(-10, -14)]
    [InlineData(20, 22)]
    [InlineData(30, 37)]
    public void RepeatedSearchResetsCachedNodeState(int start, int destination)
    {
        var world = new LineWorld { Destination = 4 };
        var search = new AStar<int>
        {
            World = world,
            OpenStorage = new Storage(),
            ClosedStorage = new Storage()
        };
        search.FindPath(0, 4, 0, 100);
        Assert.Equal(new[] { 4, 3, 2, 1 }, search.Path.ToArray());

        world.Destination = destination;
        search.FindPath(start, destination, 0, 100);
        var direction = Math.Sign(destination - start);
        var expected = Enumerable.Range(1, Math.Abs(destination - start))
            .Select(offset => start + direction * offset).Reverse().ToArray();
        Assert.Equal(expected, search.Path.ToArray());
        Assert.Equal((float)expected.Length, search.PathCost);

        world.Destination = start;
        search.FindPath(start, start, 0, 100);
        Assert.Empty(search.Path);
        Assert.Equal(0f, search.PathCost);
    }

    private sealed class LineWorld : IAStarWorld<int>
    {
        public int Destination { get; set; }

        public float Cost(int p1, int p2) => 1f;

        public void Neighbors(int p, DynamicArray<int> neighbors)
        {
            neighbors.Add(p - 1);
            neighbors.Add(p + 1);
        }

        public float Heuristic(int p1, int p2) => Math.Abs(p1 - p2);

        public bool IsGoal(int p) => p == Destination;
    }

    private sealed class Storage : IAStarStorage<int>
    {
        private readonly Dictionary<int, object?> _nodes = new();

        public void Clear() => _nodes.Clear();

        public object? Get(int p) => _nodes.GetValueOrDefault(p);

        public void Set(int p, object? data) => _nodes[p] = data;
    }
}
