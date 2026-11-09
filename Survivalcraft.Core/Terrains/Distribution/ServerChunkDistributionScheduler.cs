using EntitySystem.Core;

using Game.Network.Chunks;

namespace Game.Terrains.Distribution;

/// <summary>Prioritizes authority content requests and publishes owned logical snapshots for every connection.</summary>
public sealed class ServerChunkDistributionScheduler : IDisposable
{
    private readonly Project _project;

    private readonly WorldRuntimeContext _runtime;

    private readonly IChunkContentAuthority _authority;

    private readonly IWorldMessageSink _messages;

    private readonly Dictionary<WorldConnection, PendingChunkRequestQueue> _pending = new(
        ReferenceEqualityComparer.Instance
    );

    private readonly Dictionary<WorldConnection, ClientInterestLocation> _clientLocations =
        new(ReferenceEqualityComparer.Instance);

    private readonly Dictionary<WorldConnection, ClientMotion> _clientMotions = new(ReferenceEqualityComparer.Instance);

    private readonly Dictionary<WorldConnection, Dictionary<Point2, ChunkAllocationId>> _allocations =
        new(ReferenceEqualityComparer.Instance);

    private bool _disposed;

    public ServerChunkDistributionScheduler(Project project, IChunkContentAuthority authority)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(authority);
        _project = project;
        _runtime = WorldRuntimeContext.For(project);
        if (!_runtime.IsAuthority || _runtime.IsClosed || project.IsDisposed)
        {
            throw new InvalidOperationException("A chunk distribution scheduler requires a live authority world.");
        }

        _authority = authority;
        _messages = WorldMessageEndpoint.For(_runtime);
    }

    private bool IsClosed => _disposed || _project.IsDisposed || _runtime.IsClosed;

    public int ClientCount => _pending.Count;

    public (int Pending, int AwaitingContent) GetBacklog()
    {
        var requests = _pending.Values.SelectMany(queue => queue).ToArray();
        return (requests.Length,
            requests.Count(request => !_authority.TryGetDescriptor(request.Allocation.Coords, out _)));
    }

    public bool TryGetClientLocation(WorldConnection client, out Vector2 center, out float contentDistance)
    {
        if (_clientLocations.TryGetValue(client, out var location))
        {
            center = location.Center;
            contentDistance = location.ContentDistance;
            return true;
        }

        center = default;
        contentDistance = 0f;
        return false;
    }

    public int Enqueue(WorldConnection client, IEnumerable<ChunkContentRequest> requests)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(requests);
        if (IsClosed || !TryGetClientLocation(client, out var center, out var distance)) { return 0; }

        if (!_pending.TryGetValue(client, out var queue))
        {
            queue = new PendingChunkRequestQueue();
            _pending.Add(client, queue);
        }

        if (!_allocations.TryGetValue(client, out var allocations))
        {
            allocations = [];
            _allocations.Add(client, allocations);
        }

        var count = 0;
        foreach (var request in requests)
        {
            var allocation = request.Allocation;
            if (!NetworkTerrainPolicy.IsChunkCenterRelevant(allocation.Coords, center, distance) ||
                (allocations.TryGetValue(allocation.Coords, out var previous) &&
                 previous.Generation > allocation.Generation)) { continue; }

            allocations[allocation.Coords] = allocation;
            _messages.DiscardOlderChunkAllocations(client, allocation);
            if (queue.Enqueue(request)) { count++; }
        }

        return count;
    }

    public int GetPendingCount(WorldConnection client) => _pending.TryGetValue(client, out var queue) ? queue.Count : 0;

    public bool IsCurrentAllocation(WorldConnection client, ChunkAllocationId allocation) =>
        _allocations.TryGetValue(client, out var allocations) &&
        allocations.GetValueOrDefault(allocation.Coords) == allocation;

    public int UpdateClientLocation(WorldConnection client, Vector2 center, float contentDistance)
    {
        ArgumentNullException.ThrowIfNull(client);
        if (IsClosed) { return 0; }

        _clientLocations[client] = new ClientInterestLocation(center, contentDistance);
        UpdateClientMotion(client, center, Time.RealTime);
        if (_allocations.TryGetValue(client, out var allocations))
        {
            foreach (var coords in allocations.Keys.Where(coords =>
                         !NetworkTerrainPolicy.IsChunkCenterRelevant(coords, center, contentDistance)
                     ).ToArray())
            {
                allocations.Remove(coords);
            }
        }

        _messages.TrimChunkDelivery(client, center, contentDistance);
        return _pending.TryGetValue(client, out var queue) ? queue.RemoveOutside(center, contentDistance) : 0;
    }

    public void RemoveClient(WorldConnection client)
    {
        _pending.Remove(client);
        _clientLocations.Remove(client);
        _clientMotions.Remove(client);
        _allocations.Remove(client);
        _messages.ReleaseChunkDelivery(client);
    }

    public void OnChunkRemoved(TerrainChunk chunk)
    {
        _messages.InvalidateChunkDelivery(chunk.Coords);
    }

    public void Update()
    {
        if (IsClosed) { return; }

        foreach (var (client, queue) in _pending.ToArray())
        {
            if (!TryGetClientLocation(client, out var center, out var distance))
            {
                _pending.Remove(client);
                continue;
            }

            queue.RemoveOutside(center, distance);
            var published = 0;
            foreach (var request in queue.TakePrioritized(
                         center,
                         GetPredictedCenter(client, center),
                         TerrainChunksRequestPackage.MaximumRequests
                     ).ToArray())
            {
                if (published == 4 ||
                    !_messages.CanQueueTerrainSnapshot(client))
                {
                    break;
                }

                if (!_authority.TryGetDescriptor(request.Allocation.Coords, out _) ||
                    !_authority.TryGetSnapshot(request.Allocation.Coords, out var snapshot)) { continue; }

                var message = new TerrainSnapshotMessage(
                    ClientChunkSnapshot.Capture(request.Allocation, snapshot),
                    request.KnownContentVersion
                );
                if (!_messages.TryQueueOwnedTerrainSnapshot(client, message)) { break; }

                queue.Remove(request.Allocation.Coords);
                published++;
            }

            if (queue.Count == 0) { _pending.Remove(client); }
        }
    }

    public void Dispose()
    {
        if (_disposed) { return; }

        _disposed = true;
        foreach (var client in _clientLocations.Keys)
        {
            _messages.ReleaseChunkDelivery(client);
        }

        _pending.Clear();
        _clientLocations.Clear();
        _clientMotions.Clear();
        _allocations.Clear();
    }

    public void UpdateClientMotion(WorldConnection client, Vector2 center, double now)
    {
        if (!_clientMotions.TryGetValue(client, out var motion) || now <= motion.Time)
        {
            _clientMotions[client] = new ClientMotion(center, Vector2.Zero, now);
            return;
        }

        var elapsed = Math.Max(0.05, now - motion.Time);
        var measured = (center - motion.Center) / (float)elapsed;
        var speedSquared = measured.X * measured.X + measured.Y * measured.Y;
        var maximumSpeed = NetworkTerrainPolicy.MaximumPredictedClientSpeed;
        if (speedSquared > maximumSpeed * maximumSpeed)
        {
            measured *= maximumSpeed / MathF.Sqrt(speedSquared);
        }

        var velocity = motion.Velocity * 0.5f + measured * 0.5f;
        _clientMotions[client] = new ClientMotion(center, velocity, now);
    }

    public Vector2 GetPredictedCenter(WorldConnection client, Vector2 fallback)
    {
        return _clientMotions.TryGetValue(client, out var motion)
            ? motion.Center + motion.Velocity * NetworkTerrainPolicy.ClientPredictionSeconds
            : fallback;
    }

    private static float Priority(Point2 coords, Vector2 center, Vector2 predictedCenter)
    {
        var chunkCenter = new Vector2(
            (coords.X + 0.5f) * 16f,
            (coords.Y + 0.5f) * 16f
        );
        return Vector2.DistanceSquared(predictedCenter, chunkCenter) * 0.7f +
               Vector2.DistanceSquared(center, chunkCenter) * 0.3f;
    }

    private readonly record struct ClientInterestLocation(Vector2 Center, float ContentDistance);

    private readonly record struct ClientMotion(Vector2 Center, Vector2 Velocity, double Time);
}
