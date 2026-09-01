using Game.Terrains.Distribution;

namespace Game.Network;

/// <summary>
///     保持首次请求顺序的区块请求去重队列。
/// </summary>
public sealed class PendingChunkRequestQueue : IEnumerable<ChunkContentRequest>
{
    private readonly LinkedList<ChunkContentRequest> _ordered = [];

    private readonly Dictionary<Point2, LinkedListNode<ChunkContentRequest>> _nodes = [];

    public int Count => _nodes.Count;

    public bool Enqueue(ChunkContentRequest request)
    {
        var coords = request.Allocation.Coords;
        if (_nodes.TryGetValue(coords, out var existing))
        {
            if (request.Allocation.Generation >= existing.Value.Allocation.Generation)
            {
                existing.Value = request;
            }

            return false;
        }

        var node = _ordered.AddLast(request);
        _nodes.Add(coords, node);
        return true;
    }

    public int EnqueueRange(IEnumerable<ChunkContentRequest> requests)
    {
        return requests.Count(Enqueue);
    }

    public bool Remove(Point2 coords)
    {
        if (!_nodes.Remove(coords, out var node))
        {
            return false;
        }

        _ordered.Remove(node);
        return true;
    }

    public bool TryGet(Point2 coords, out ChunkContentRequest request)
    {
        if (_nodes.TryGetValue(coords, out var node))
        {
            request = node.Value;
            return true;
        }

        request = default;
        return false;
    }

    public int RemoveOutside(Vector2 center, float contentDistance)
    {
        var maximumDistanceSquared = MathUtils.Sqr(contentDistance + 12f);
        var removed = 0;
        foreach (var request in _ordered.ToArray())
        {
            var coords = request.Allocation.Coords;
            var chunkCenter = new Vector2((coords.X + 0.5f) * 16f, (coords.Y + 0.5f) * 16f);
            if (Vector2.DistanceSquared(center, chunkCenter) <= maximumDistanceSquared)
            {
                continue;
            }

            if (Remove(coords))
            {
                removed++;
            }
        }

        return removed;
    }

    public IEnumerable<ChunkContentRequest> TakeNearest(Vector2 center, int count) =>
        TakePrioritized(center, center, count);

    public IEnumerable<ChunkContentRequest> TakePrioritized(
        Vector2 center,
        Vector2 predictedCenter,
        int count) =>
        _ordered
            .OrderBy(request =>
            {
                var coords = request.Allocation.Coords;
                var chunkCenter = new Vector2((coords.X + 0.5f) * 16f, (coords.Y + 0.5f) * 16f);
                return Vector2.DistanceSquared(predictedCenter, chunkCenter) * 0.7f +
                       Vector2.DistanceSquared(center, chunkCenter) * 0.3f;
            })
            .Take(count);

    public IEnumerator<ChunkContentRequest> GetEnumerator() => _ordered.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}
