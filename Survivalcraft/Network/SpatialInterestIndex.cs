namespace Game.Network;

internal sealed class SpatialInterestIndex
{
    public Dictionary<Client, HashSet<int>> GetPointCandidates(IEnumerable<(int Id, Vector2 Position)> points)
    {
        var candidates = new Dictionary<Client, HashSet<int>>(ReferenceEqualityComparer.Instance);
        foreach (var (id, position) in points)
        {
            foreach (var client in GetObservers(position))
            {
                if (!candidates.TryGetValue(client, out var ids))
                {
                    ids = [];
                    candidates.Add(client, ids);
                }

                ids.Add(id);
            }
        }

        return candidates;
    }

    private const int _bucketSize = 64;

    private readonly Dictionary<Client, Location> _locations = new(ReferenceEqualityComparer.Instance);

    private readonly Dictionary<Point2, HashSet<Client>> _coverage = [];

    private readonly Dictionary<Point2, HashSet<Client>> _centers = [];

    public void SetLocation(Client client, Vector2 center, float distance)
    {
        if (!float.IsFinite(center.X) || !float.IsFinite(center.Y) || !float.IsFinite(distance) || distance < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(distance));
        }

        var location = new Location(center, distance);
        if (_locations.TryGetValue(client, out var previous))
        {
            if (previous == location)
            {
                return;
            }

            RemoveLocation(client, previous);
        }

        _locations[client] = location;
        Add(_centers, Bucket(center), client);
        foreach (var bucket in CoveredBuckets(location))
        {
            Add(_coverage, bucket, client);
        }
    }

    public void RetainClients(IEnumerable<Client> clients)
    {
        var active = new HashSet<Client>(clients, ReferenceEqualityComparer.Instance);
        foreach (var client in _locations.Keys.Where(client => !active.Contains(client)).ToArray())
        {
            RemoveLocation(client, _locations[client]);
            _locations.Remove(client);
        }
    }

    public IEnumerable<Client> GetObservers(Vector2 point)
    {
        if (!_coverage.TryGetValue(Bucket(point), out var candidates))
        {
            yield break;
        }

        foreach (var client in candidates)
        {
            var location = _locations[client];
            if (Vector2.DistanceSquared(point, location.Center) <= location.Distance * location.Distance)
            {
                yield return client;
            }
        }
    }

    public IEnumerable<Client> GetChunkObservers(Point2 chunk)
    {
        if (!_coverage.TryGetValue(new Point2(chunk.X >> 2, chunk.Y >> 2), out var candidates))
        {
            yield break;
        }

        foreach (var client in candidates)
        {
            var location = _locations[client];
            if (NetworkTerrainPolicy.IsChunkRelevant(chunk, location.Center, location.Distance))
            {
                yield return client;
            }
        }
    }

    public IEnumerable<Client> GetObserversWithin(Vector2 point, float radius)
    {
        if (!float.IsFinite(radius) || radius < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(radius));
        }

        var min = Bucket(point - new Vector2(radius));
        var max = Bucket(point + new Vector2(radius));
        var bucketCount = ((double)max.X - min.X + 1) * ((double)max.Y - min.Y + 1);
        return bucketCount > _centers.Count
            ? GetSparseRadiusObservers(point, radius * radius)
            : GetGridRadiusObservers(point, radius * radius, min, max);
    }

    private IEnumerable<Client> GetSparseRadiusObservers(Vector2 point, float radiusSquared)
    {
        // Reuse authoritative index records; avoid walking a mostly empty grid or looking up each member again.
        foreach (var (client, location) in _locations)
        {
            if (Vector2.DistanceSquared(point, location.Center) <= radiusSquared)
            {
                yield return client;
            }
        }
    }

    private IEnumerable<Client> GetGridRadiusObservers(Vector2 point, float radiusSquared, Point2 min, Point2 max)
    {
        for (var x = min.X; x <= max.X; x++)
        {
            for (var y = min.Y; y <= max.Y; y++)
            {
                if (!_centers.TryGetValue(new Point2(x, y), out var candidates))
                {
                    continue;
                }

                foreach (var client in candidates)
                {
                    if (Vector2.DistanceSquared(point, _locations[client].Center) <= radiusSquared)
                    {
                        yield return client;
                    }
                }
            }
        }
    }

    private void RemoveLocation(Client client, Location location)
    {
        Remove(_centers, Bucket(location.Center), client);
        foreach (var bucket in CoveredBuckets(location))
        {
            Remove(_coverage, bucket, client);
        }
    }

    private static IEnumerable<Point2> CoveredBuckets(Location location)
    {
        var min = Bucket(location.Center - new Vector2(location.Distance));
        var max = Bucket(location.Center + new Vector2(location.Distance));
        // Include the bucket immediately before an exact lower boundary: chunk rectangles are closed.
        min = new Point2(min.X - 1, min.Y - 1);
        for (var x = min.X; x <= max.X; x++)
        {
            for (var y = min.Y; y <= max.Y; y++)
            {
                if (Intersects(location, new Vector2(x * (float)_bucketSize, y * (float)_bucketSize), _bucketSize))
                {
                    yield return new Point2(x, y);
                }
            }
        }
    }

    private static bool Intersects(Location location, Vector2 min, int size)
    {
        var closest = new Vector2(MathUtils.Clamp(location.Center.X, min.X, min.X + size),
            MathUtils.Clamp(location.Center.Y, min.Y, min.Y + size));
        return Vector2.DistanceSquared(location.Center, closest) <= location.Distance * location.Distance;
    }

    private static Point2 Bucket(Vector2 point)
    {
        return new Point2((int)MathF.Floor(point.X / _bucketSize), (int)MathF.Floor(point.Y / _bucketSize));
    }

    private static void Add(Dictionary<Point2, HashSet<Client>> index, Point2 bucket, Client client)
    {
        if (!index.TryGetValue(bucket, out var clients))
        {
            clients = new HashSet<Client>(ReferenceEqualityComparer.Instance);
            index.Add(bucket, clients);
        }

        clients.Add(client);
    }

    private static void Remove(Dictionary<Point2, HashSet<Client>> index, Point2 bucket, Client client)
    {
        var clients = index[bucket];
        clients.Remove(client);
        if (clients.Count == 0)
        {
            index.Remove(bucket);
        }
    }

    private readonly record struct Location(Vector2 Center, float Distance);
}
