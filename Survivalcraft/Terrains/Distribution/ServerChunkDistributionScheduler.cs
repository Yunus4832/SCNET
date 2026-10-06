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

    private readonly Dictionary<Client, PendingChunkRequestQueue> _pending = new(ReferenceEqualityComparer.Instance);

    private readonly Dictionary<Client, ClientInterestLocation> _clientLocations =
        new(ReferenceEqualityComparer.Instance);

    private readonly Dictionary<Client, ClientMotion> _clientMotions = new(ReferenceEqualityComparer.Instance);

    private readonly Dictionary<Client, Dictionary<Point2, TerrainChunkFragmentRequest>> _missing =
        new(ReferenceEqualityComparer.Instance);

    private readonly List<Client> _clientsToRemove = [];

    private long _fragmentsRetransmitted;

    private long _fragmentBytesRetransmitted;

    public int ClientCount => _pending.Count;

    public long FragmentsRetransmitted => Interlocked.Read(ref _fragmentsRetransmitted);

    public long FragmentBytesRetransmitted => Interlocked.Read(ref _fragmentBytesRetransmitted);

    internal (int Pending, int AwaitingContent, int OutstandingEncodes) GetBacklog()
    {
        var pending = 0;
        var awaitingContent = 0;
        foreach (var queue in _pending.Values)
        {
            foreach (var request in queue)
            {
                pending++;
                if (!_authority.TryGetDescriptor(request.Allocation.Coords, out _))
                {
                    awaitingContent++;
                }
            }
        }

        return (pending, awaitingContent, _encoder.OutstandingCount);
    }

    public bool TryGetClientLocation(Client client, out Vector2 center, out float contentDistance)
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

    public int Enqueue(Client client, IEnumerable<ChunkContentRequest> requests)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(requests);
        if (!TryGetClientLocation(client, out var center, out var contentDistance))
        {
            return 0;
        }

        var allowed = requests.Where(request =>
            NetworkTerrainPolicy.IsChunkRelevant(request.Allocation.Coords, center, contentDistance));
        if (_pending.TryGetValue(client, out var queue))
        {
            return queue.EnqueueRange(allowed);
        }

        queue = new PendingChunkRequestQueue();
        _pending.Add(client, queue);

        return queue.EnqueueRange(allowed);
    }

    public int GetPendingCount(Client client) =>
        (_pending.TryGetValue(client, out var queue) ? queue.Count : 0) +
        (_missing.TryGetValue(client, out var missing) ? missing.Count : 0);

    public int UpdateClientLocation(Client client, Vector2 center, float contentDistance)
    {
        ArgumentNullException.ThrowIfNull(client);
        _clientLocations[client] = new ClientInterestLocation(center, contentDistance);
        UpdateClientMotion(client, center, Time.RealTime);
        var removed = _pending.TryGetValue(client, out var queue)
            ? queue.RemoveOutside(center, contentDistance)
            : 0;
        if (!_missing.TryGetValue(client, out var missing))
        {
            return removed;
        }

        foreach (var coords in missing.Keys.Where(coords =>
                     !NetworkTerrainPolicy.IsChunkRelevant(coords, center, contentDistance)).ToArray())
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
        if (!TryGetClientLocation(client, out var center, out var contentDistance))
        {
            return 0;
        }

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
            if (!NetworkTerrainPolicy.IsChunkRelevant(request.Allocation.Coords, center, contentDistance))
            {
                continue;
            }

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
        _clientLocations.Remove(client);
        _clientMotions.Remove(client);
        _missing.Remove(client);
    }

    public void OnChunkRemoved(TerrainChunk chunk)
    {
        _cache.Remove(chunk.Coords);
    }

    public void Update() => Update(Time.PeriodicEvent(1, 0.6));

    internal void Update(bool sendWindow)
    {
        DrainCompletedEncodes();
        foreach (var item in _pending)
        {
            if (!TryGetClientLocation(item.Key, out var center, out var contentDistance))
            {
                _clientsToRemove.Add(item.Key);
                continue;
            }

            item.Value.RemoveOutside(center, contentDistance);
            var toRemove = new List<Point2>();
            var requests = item.Value.TakePrioritized(
                    center,
                    GetPredictedCenter(item.Key, center),
                    SettingsManager.Current.ServerChunkCountSendPer)
                .ToArray();
            var cachedCount = 0;
            var cachedBytes = 0;
            SendMissingFragments(item.Key, center, sendWindow, ref cachedCount, ref cachedBytes);
            foreach (var request in requests)
            {
                var coords = request.Allocation.Coords;
                if (_authority.TryGetDescriptor(coords, out var descriptor))
                {
                    if (_cache.TryGet(coords, descriptor.ContentVersion, out var encoded))
                    {
                        if (!sendWindow)
                        {
                            continue;
                        }

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
                            CommonLib.Net.QueuePackage(
                                new SubsystemTerrainPackage(fragment),
                                PackageAudience.To(item.Key));
                        }

                        toRemove.Add(coords);
                        cachedCount++;
                        cachedBytes += transmissionBytes;
                    }
                    else if (_encoder.CanSchedule(descriptor) &&
                             _authority.TryGetSnapshot(coords, out var snapshot))
                    {
                        _encoder.TrySchedule(snapshot);
                    }
                }
                // Missing descriptors also mean generation is still in progress.
                // Keep the request until content is ready or interest moves away;
                // rejecting it would add a client retry delay to normal generation.
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
        _clientLocations.Clear();
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
        bool sendWindow,
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
            if (!TryGetClientLocation(client, out var approvedCenter, out var distance) ||
                !NetworkTerrainPolicy.IsChunkRelevant(coords, approvedCenter, distance))
            {
                requests.Remove(coords);
                continue;
            }

            if (!_authority.TryGetDescriptor(coords, out var descriptor))
            {
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
                if (_encoder.CanSchedule(descriptor) && _authority.TryGetSnapshot(coords, out var snapshot))
                {
                    _encoder.TrySchedule(snapshot);
                }

                continue;
            }

            if (!sendWindow)
            {
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
                CommonLib.Net.QueuePackage(
                    new SubsystemTerrainPackage(fragment),
                    PackageAudience.To(client));
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

    private void RemoveEmptyClients()
    {
        foreach (var client in _clientsToRemove)
        {
            _pending.Remove(client);
            _missing.Remove(client);
        }

        _clientsToRemove.Clear();
    }

    private readonly record struct ClientInterestLocation(Vector2 Center, float ContentDistance);

    private readonly record struct ClientMotion(Vector2 Center, Vector2 Velocity, double Time);
}
