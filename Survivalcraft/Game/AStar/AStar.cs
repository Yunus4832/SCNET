namespace Game;

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
        var node = NewNode(start, default, 0f, 0f);
        OpenStorage.Set(start, node);
        HeapEnqueue(node);
        Node? node2 = null;
        var num = 0;
        Node? node3;
        while (true)
        {
            node3 = _openHeap.Count > 0 ? HeapDequeue() : null;
            if (node3 == null || num >= maxPositionsToCheck)
            {
                if (node2 != null)
                {
                    BuildPathFromEndNode(node, node2);
                    return;
                }

                Path.Clear();
                PathCost = 0f;
                return;
            }

            if (World.IsGoal(node3.Position))
            {
                break;
            }

            ClosedStorage.Set(node3.Position, node3);
            OpenStorage.Set(node3.Position, null);
            num++;
            _neighbors.Clear();
            World.Neighbors(node3.Position, _neighbors);
            for (var i = 0; i < _neighbors.Count; i++)
            {
                var val = _neighbors.Array[i];
                if (ClosedStorage.Get(val) != null)
                {
                    continue;
                }

                var num2 = World.Cost(node3.Position, val);
                if (num2.CloseTo(1f / 0f))
                {
                    continue;
                }

                var num3 = node3.G + num2;
                var num4 = World.Heuristic(val, end);
                if (node3 != node && (node2 == null || num4 < node2.H))
                {
                    node2 = node3;
                }

                if (OpenStorage.Get(val) is Node node4)
                {
                    if (!(num3 < node4.G))
                    {
                        continue;
                    }

                    node4.G = num3;
                    node4.F = num3 + node4.H;
                    node4.PreviousPosition = node3.Position;
                    HeapUpdate(node4);
                }
                else
                {
                    node4 = NewNode(val, node3.Position, num3, num4);
                    OpenStorage.Set(val, node4);
                    HeapEnqueue(node4);
                }
            }
        }

        BuildPathFromEndNode(node, node3);
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
            var num = pos;
            var num2 = 2 * pos + 1;
            var num3 = 2 * pos + 2;
            if (num2 < _openHeap.Count && _openHeap.Array[num2].F < _openHeap.Array[num].F)
            {
                num = num2;
            }

            if (num3 < _openHeap.Count && _openHeap.Array[num3].F < _openHeap.Array[num].F)
            {
                num = num3;
            }

            if (num != pos)
            {
                (_openHeap.Array[num], _openHeap.Array[pos]) = (_openHeap.Array[pos], _openHeap.Array[num]);
                pos = num;
                continue;
            }

            break;
        }
    }

    public void HeapifyFromPosToStart(int pos)
    {
        var num = pos;
        while (num > 0)
        {
            var num2 = (num - 1) / 2;
            var node = _openHeap.Array[num2];
            var node2 = _openHeap.Array[num];
            if (node.F > node2.F)
            {
                _openHeap.Array[num2] = node2;
                _openHeap.Array[num] = node;
                num = num2;
                continue;
            }

            break;
        }
    }

    public Node NewNode(T position, T previousPosition, float g, float h)
    {
        while (_nodesCacheIndex >= _nodesCache.Count)
        {
            _nodesCache.Add(new Node
                {
                    Position = position,
                    PreviousPosition = previousPosition,
                    F = g + h,
                    G = g,
                    H = h
                }
            );
        }

        return _nodesCache.Array[_nodesCacheIndex++];
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
