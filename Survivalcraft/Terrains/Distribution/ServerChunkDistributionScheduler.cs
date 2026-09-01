using Game.Network;
using Game.Network.Packages;
using Game.Network.Serialization;

namespace Game.Terrains.Distribution;

/// <summary>
///     Owns server-side chunk request deduplication, encoding backpressure, caching and delivery.
/// </summary>
public sealed class ServerChunkDistributionScheduler(
    IChunkContentAuthority authority,
    int maximumOutstandingEncodes)
    : IDisposable
{
    private readonly IChunkContentAuthority
        _authority = authority ?? throw new ArgumentNullException(nameof(authority));

    private readonly NetworkChunkCache _cache = new();

    private readonly NetworkChunkEncoder _encoder = new(maximumOutstandingEncodes);

    private readonly Dictionary<Client, PendingChunkRequestQueue> _pending = [];

    private readonly Dictionary<Client, Vector2> _clientCenters = [];

    private readonly Dictionary<Client, ClientMotion> _clientMotions = [];

    private readonly Dictionary<Client, Dictionary<Point2, TerrainChunkFragmentRequest>> _missing = [];

    private readonly List<Client> _clientsToRemove = [];

    private long _fragmentsRetransmitted;

    private long _fragmentBytesRetransmitted;

    public int ClientCount => _pending.Count;

    public long FragmentsRetransmitted => Interlocked.Read(ref _fragmentsRetransmitted);

    public long FragmentBytesRetransmitted => Interlocked.Read(ref _fragmentBytesRetransmitted);

    public int Enqueue(Client client, IEnumerable<ChunkContentRequest> requests)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(requests);
        if (_pending.TryGetValue(client, out var queue))
        {
            return queue.EnqueueRange(requests);
        }

        queue = new PendingChunkRequestQueue();
        _pending.Add(client, queue);

        return queue.EnqueueRange(requests);
    }

    public int GetPendingCount(Client client) =>
        (_pending.TryGetValue(client, out var queue) ? queue.Count : 0) +
        (_missing.TryGetValue(client, out var missing) ? missing.Count : 0);

    public int UpdateClientLocation(Client client, Vector2 center, float contentDistance)
    {
        ArgumentNullException.ThrowIfNull(client);
        _clientCenters[client] = center;
        UpdateClientMotion(client, center, Time.RealTime);
        var removed = _pending.TryGetValue(client, out var queue)
            ? queue.RemoveOutside(center, contentDistance)
            : 0;
        if (!_missing.TryGetValue(client, out var missing))
        {
            return removed;
        }

        foreach (var coords in missing.Keys.Where(coords =>
                     !IsWithinDistance(coords, center, contentDistance + 12f)).ToArray())
        {
            missing.Remove(coords);
            removed++;
        }

        return removed;
    }

    public int EnqueueMissing(
        Client client,
        IEnumerable<TerrainChunkFragmentRequest> requests)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(requests);
        if (!_missing.TryGetValue(client, out var queue))
        {
            queue = [];
            _missing.Add(client, queue);
        }

        if (!_pending.ContainsKey(client))
        {
            _pending.Add(client, new PendingChunkRequestQueue());
        }

        var added = 0;
        foreach (var request in requests)
        {
            if (!queue.ContainsKey(request.Allocation.Coords))
            {
                added++;
            }

            queue[request.Allocation.Coords] = request;
        }

        return added;
    }

    public void RemoveClient(Client client)
    {
        ArgumentNullException.ThrowIfNull(client);
        _pending.Remove(client);
        _clientCenters.Remove(client);
        _clientMotions.Remove(client);
        _missing.Remove(client);
    }

    public void OnChunkRemoved(TerrainChunk chunk)
    {
        _cache.Remove(chunk.Coords);
    }

    public void Update()
    {
        DrainCompletedEncodes();
        foreach (var item in _pending)
        {
            var toRemove = new List<Point2>();
            var failures = new List<ChunkAllocationId>();
            if (Time.PeriodicEvent(1, 0.6))
            {
                var requests = (_clientCenters.TryGetValue(item.Key, out var center)
                        ? item.Value.TakePrioritized(
                            center,
                            GetPredictedCenter(item.Key, center),
                            SettingsManager.Current.ServerChunkCountSendPer)
                        : item.Value.Take(SettingsManager.Current.ServerChunkCountSendPer))
                    .ToArray();
                var cachedCount = 0;
                var cachedBytes = 0;
                SendMissingFragments(item.Key, center, ref cachedCount, ref cachedBytes);
                foreach (var request in requests)
                {
                    var coords = request.Allocation.Coords;
                    if (_authority.TryGetDescriptor(coords, out var descriptor))
                    {
                        if (_cache.TryGet(coords, descriptor.ContentVersion, out var encoded))
                        {
                            var transmissionBytes = encoded.Payload.Length;
                            var byteBudget = Math.Max(1,
                                SettingsManager.Current.ServerChunkBytesSendPerSecond);
                            if (cachedCount > 0 && cachedBytes + transmissionBytes > byteBudget)
                            {
                                break;
                            }

                            foreach (var fragment in EncodedTerrainChunkFragmenter.Split(
                                         encoded,
                                         request.Allocation))
                            {
                                CommonLib.Net.QueuePackage(new SubsystemTerrainPackage(fragment)
                                {
                                    To = item.Key
                                });
                            }

                            toRemove.Add(coords);
                            cachedCount++;
                            cachedBytes += transmissionBytes;
                        }
                        else if (!_encoder.IsScheduled(descriptor) &&
                                 _authority.TryGetSnapshot(coords, out var snapshot))
                        {
                            _encoder.TrySchedule(snapshot);
                        }
                    }
                    else
                    {
                        failures.Add(request.Allocation);
                        toRemove.Add(coords);
                    }
                }

                if (failures.Count > 0)
                {
                    CommonLib.Net.QueuePackage(new SubsystemTerrainPackage(failures, 0) { To = item.Key });
                }
            }

            foreach (var coords in toRemove)
            {
                item.Value.Remove(coords);
            }

            if (item.Value.Count == 0 &&
                (!_missing.TryGetValue(item.Key, out var missing) || missing.Count == 0))
            {
                _clientsToRemove.Add(item.Key);
            }
        }

        RemoveEmptyClients();
    }

    public void Dispose()
    {
        _encoder.Dispose();
        _pending.Clear();
        _clientCenters.Clear();
        _clientMotions.Clear();
        _missing.Clear();
        _clientsToRemove.Clear();
    }

    private void DrainCompletedEncodes()
    {
        _encoder.DrainCompleted(_cache);
    }

    internal void UpdateClientMotion(Client client, Vector2 center, double now)
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

    internal Vector2 GetPredictedCenter(Client client, Vector2 fallback) =>
        _clientMotions.TryGetValue(client, out var motion)
            ? motion.Center + motion.Velocity * NetworkTerrainPolicy.ClientPredictionSeconds
            : fallback;

    private void SendMissingFragments(
        Client client,
        Vector2 center,
        ref int sentCount,
        ref int sentBytes)
    {
        if (!_missing.TryGetValue(client, out var requests) || requests.Count == 0)
        {
            return;
        }

        var predicted = GetPredictedCenter(client, center);
        foreach (var request in requests.Values
                     .OrderBy(request => FragmentPriority(request, center, predicted))
                     .ToArray())
        {
            var coords = request.Allocation.Coords;
            if (!_authority.TryGetDescriptor(coords, out var descriptor))
            {
                requests.Remove(coords);
                continue;
            }

            if (descriptor.ContentVersion != request.ContentVersion)
            {
                Enqueue(client, [new ChunkContentRequest(request.Allocation, request.ContentVersion)]);
                requests.Remove(coords);
                continue;
            }

            if (!_cache.TryGet(coords, descriptor.ContentVersion, out var encoded))
            {
                if (!_encoder.IsScheduled(descriptor) && _authority.TryGetSnapshot(coords, out var snapshot))
                {
                    _encoder.TrySchedule(snapshot);
                }

                continue;
            }

            if (!TrySelectMissingFragments(encoded, request, out var fragments))
            {
                Enqueue(client, [new ChunkContentRequest(request.Allocation, request.ContentVersion)]);
                requests.Remove(coords);
                continue;
            }

            var transmissionBytes = fragments.Sum(fragment => fragment.Payload.Length);
            var byteBudget = Math.Max(1, SettingsManager.Current.ServerChunkBytesSendPerSecond);
            if (sentCount > 0 && sentBytes + transmissionBytes > byteBudget)
            {
                break;
            }

            foreach (var fragment in fragments)
            {
                CommonLib.Net.QueuePackage(new SubsystemTerrainPackage(fragment) { To = client });
            }

            Interlocked.Add(ref _fragmentsRetransmitted, fragments.Length);
            Interlocked.Add(ref _fragmentBytesRetransmitted, transmissionBytes);
            requests.Remove(coords);
            sentCount++;
            sentBytes += transmissionBytes;
        }
    }

    internal static bool TrySelectMissingFragments(
        EncodedTerrainChunk encoded,
        TerrainChunkFragmentRequest request,
        out EncodedTerrainChunkFragment[] selected)
    {
        ArgumentNullException.ThrowIfNull(encoded);
        var fragments = EncodedTerrainChunkFragmenter.Split(encoded, request.Allocation).ToArray();
        if (encoded.ContentVersion != request.ContentVersion ||
            fragments.Length != request.FragmentCount ||
            request.MissingFragmentIndices.Length == 0 ||
            request.MissingFragmentIndices.Any(index => index >= fragments.Length))
        {
            selected = [];
            return false;
        }

        selected = request.MissingFragmentIndices
            .Distinct()
            .Select(index => fragments[index])
            .ToArray();
        return true;
    }

    private static float FragmentPriority(
        TerrainChunkFragmentRequest request,
        Vector2 center,
        Vector2 predictedCenter)
    {
        var chunkCenter = new Vector2(
            (request.Allocation.Coords.X + 0.5f) * 16f,
            (request.Allocation.Coords.Y + 0.5f) * 16f);
        return Vector2.DistanceSquared(predictedCenter, chunkCenter) * 0.7f +
               Vector2.DistanceSquared(center, chunkCenter) * 0.3f;
    }

    private static bool IsWithinDistance(Point2 coords, Vector2 center, float distance)
    {
        var chunkCenter = new Vector2((coords.X + 0.5f) * 16f, (coords.Y + 0.5f) * 16f);
        return Vector2.DistanceSquared(center, chunkCenter) <= MathUtils.Sqr(distance);
    }

    private void RemoveEmptyClients()
    {
        foreach (var client in _clientsToRemove)
        {
            _pending.Remove(client);
            _clientCenters.Remove(client);
            _clientMotions.Remove(client);
            _missing.Remove(client);
        }

        _clientsToRemove.Clear();
    }

    private readonly record struct ClientMotion(Vector2 Center, Vector2 Velocity, double Time);
}
