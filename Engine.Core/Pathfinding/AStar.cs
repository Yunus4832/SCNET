using Engine.Core.Collections;
using Engine.Core.Mathematics;

namespace Engine.Core.Pathfinding;

public class AStar<T> where T : unmanaged
{
    private readonly DynamicArray<T> _neighbors = [];

    private readonly DynamicArray<Node> _nodesCache = [];

    private int _nodesCacheIndex;

    private readonly DynamicArray<Node> _openHeap = [];

    public float PathCost { get; set; }

    public DynamicArray<T> Path { get; set; } = [];

    public IAStarWorld<T>? World { get; set; }

    public required IAStarStorage<T> OpenStorage { get; set; }

    public required IAStarStorage<T> ClosedStorage { get; set; }

    public void BuildPathFromEndNode(Node startNode, Node endNode)
    {
        PathCost = endNode.G;
        Path.Clear();
        var node = endNode;
        while (node != startNode)
        {
            Path.Add(node.Position);

            if (ClosedStorage.Get(node.PreviousPosition) is not Node previousNode)
            {
                throw new InvalidOperationException("Cannot found path");
            }

            node = previousNode;
        }
    }

    public void FindPath(T start, T end, float minHeuristic, int maxPositionsToCheck)
    {
        if (Path == null)
        {
            throw new InvalidOperationException("Path not specified.");
        }

        if (World == null)
        {
            throw new InvalidOperationException("AStar World not specified.");
        }

        if (OpenStorage == null)
        {
            throw new InvalidOperationException("AStar OpenStorage not specified.");
        }

        if (OpenStorage == null)
        {
            throw new InvalidOperationException("AStar ClosedStorage not specified.");
        }

        _nodesCacheIndex = 0;
        _openHeap.Clear();
        OpenStorage.Clear();
        ClosedStorage.Clear();
        var startNode = NewNode(start, default, 0f, 0f);
        OpenStorage.Set(start, startNode);
        HeapEnqueue(startNode);
        Node? fallbackNode = null;
        var expandedCount = 0;
        Node? currentNode;
        while (true)
        {
            currentNode = _openHeap.Count > 0 ? HeapDequeue() : null;
            if (currentNode == null || expandedCount >= maxPositionsToCheck)
            {
                if (fallbackNode != null)
                {
                    BuildPathFromEndNode(startNode, fallbackNode);
                    return;
                }

                Path.Clear();
                PathCost = 0f;
                return;
            }

            if (World.IsGoal(currentNode.Position))
            {
                break;
            }

            ClosedStorage.Set(currentNode.Position, currentNode);
            OpenStorage.Set(currentNode.Position, null);
            expandedCount++;
            _neighbors.Clear();
            World.Neighbors(currentNode.Position, _neighbors);
            for (var i = 0; i < _neighbors.Count; i++)
            {
                var neighborPosition = _neighbors.Array[i];
                if (ClosedStorage.Get(neighborPosition) != null)
                {
                    continue;
                }

                var stepCost = World.Cost(currentNode.Position, neighborPosition);
                if (stepCost.CloseTo(1f / 0f))
                {
                    continue;
                }

                var candidateCost = currentNode.G + stepCost;
                var estimatedRemainingCost = World.Heuristic(neighborPosition, end);
                if (currentNode != startNode && (fallbackNode == null || estimatedRemainingCost < fallbackNode.H))
                {
                    fallbackNode = currentNode;
                }

                if (OpenStorage.Get(neighborPosition) is Node neighborNode)
                {
                    if (!(candidateCost < neighborNode.G))
                    {
                        continue;
                    }

                    neighborNode.G = candidateCost;
                    neighborNode.F = candidateCost + neighborNode.H;
                    neighborNode.PreviousPosition = currentNode.Position;
                    HeapUpdate(neighborNode);
                }
                else
                {
                    neighborNode = NewNode(
                        neighborPosition,
                        currentNode.Position,
                        candidateCost,
                        estimatedRemainingCost
                    );
                    OpenStorage.Set(neighborPosition, neighborNode);
                    HeapEnqueue(neighborNode);
                }
            }
        }

        BuildPathFromEndNode(startNode, currentNode);
    }

    public void HeapEnqueue(Node node)
    {
        _openHeap.Add(node);
        HeapifyFromPosToStart(_openHeap.Count - 1);
    }

    public Node HeapDequeue()
    {
        var result = _openHeap.Array[0];
        if (_openHeap.Count <= 1)
        {
            _openHeap.Clear();
            return result;
        }

        _openHeap.Array[0] = _openHeap.Array[_openHeap.Count - 1];
        _ = --_openHeap.Count;
        HeapifyFromPosToEnd(0);
        return result;
    }

    public void HeapUpdate(Node node)
    {
        var pos = -1;
        for (var i = 0; i < _openHeap.Count; i++)
        {
            if (_openHeap.Array[i] == node)
            {
                pos = i;
                break;
            }
        }

        HeapifyFromPosToStart(pos);
    }

    public void HeapifyFromPosToEnd(int pos)
    {
        while (true)
        {
            var lowestCostIndex = pos;
            var leftChildIndex = 2 * pos + 1;
            var rightChildIndex = 2 * pos + 2;
            if (leftChildIndex < _openHeap.Count &&
                _openHeap.Array[leftChildIndex].F < _openHeap.Array[lowestCostIndex].F)
            {
                lowestCostIndex = leftChildIndex;
            }

            if (rightChildIndex < _openHeap.Count &&
                _openHeap.Array[rightChildIndex].F < _openHeap.Array[lowestCostIndex].F)
            {
                lowestCostIndex = rightChildIndex;
            }

            if (lowestCostIndex != pos)
            {
                (_openHeap.Array[lowestCostIndex], _openHeap.Array[pos]) =
                    (_openHeap.Array[pos], _openHeap.Array[lowestCostIndex]);
                pos = lowestCostIndex;
                continue;
            }

            break;
        }
    }

    public void HeapifyFromPosToStart(int pos)
    {
        var childIndex = pos;
        while (childIndex > 0)
        {
            var parentIndex = (childIndex - 1) / 2;
            var parent = _openHeap.Array[parentIndex];
            var child = _openHeap.Array[childIndex];
            if (parent.F > child.F)
            {
                _openHeap.Array[parentIndex] = child;
                _openHeap.Array[childIndex] = parent;
                childIndex = parentIndex;
                continue;
            }

            break;
        }
    }

    public Node NewNode(T position, T previousPosition, float g, float h)
    {
        while (_nodesCacheIndex >= _nodesCache.Count)
        {
            _nodesCache.Add(new Node());
        }

        var node = _nodesCache.Array[_nodesCacheIndex++];
        node.Position = position;
        node.PreviousPosition = previousPosition;
        node.F = g + h;
        node.G = g;
        node.H = h;
        return node;
    }

    public class Node
    {
        public float F;

        public float G;

        public float H;

        public T Position;

        public T PreviousPosition;
    }
}
