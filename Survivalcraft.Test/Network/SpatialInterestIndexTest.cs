using Engine.Core;

using Game.Network;

using Random = System.Random;

namespace Survivalcraft.Test.Network;

public sealed class SpatialInterestIndexTest
{
    [Fact]
    public void PointCandidateBatchUsesConnectionIdentityAndTracksChangedPositions()
    {
        var index = new SpatialInterestIndex();
        var first = CreateClient();
        var second = CreateClient();
        index.SetLocation(first, Vector2.Zero, 16);
        index.SetLocation(second, new Vector2(1024, 0), 16);
        var candidates = index.GetPointCandidates([(1, Vector2.Zero), (1, Vector2.Zero),
            (2, new Vector2(1024, 0)), (3, new Vector2(512, 0))]);

        Assert.Equal(2, candidates.Count);
        Assert.Equal([1], candidates[first]);
        Assert.Equal([2], candidates[second]);
        var moved = index.GetPointCandidates([(1, new Vector2(1024, 0))]);
        Assert.False(moved.ContainsKey(first));
        Assert.Equal([1], moved[second]);
        index.RetainClients([first]);
        Assert.Empty(index.GetPointCandidates([(1, new Vector2(1024, 0))]));
    }

    [Fact]
    public void CircularPointAndClosedChunkBoundariesMatchAtNegativeCoordinates()
    {
        var index = new SpatialInterestIndex();
        var client = CreateClient();
        index.SetLocation(client, new Vector2(-64, -64), 16);

        Assert.Same(client, Assert.Single(index.GetObservers(new Vector2(-80, -64))));
        Assert.Empty(index.GetObservers(new Vector2(-80, -80)));
        Assert.Same(client, Assert.Single(index.GetChunkObservers(new Point2(-6, -4))));
        Assert.Empty(index.GetChunkObservers(new Point2(-6, -6)));
    }

    [Fact]
    public void TeleportAndDistanceChangeRemoveOldCoverageWithoutDuplicates()
    {
        var index = new SpatialInterestIndex();
        var client = CreateClient();
        index.SetLocation(client, Vector2.Zero, 128);
        index.SetLocation(client, Vector2.Zero, 128);
        Assert.Same(client, Assert.Single(index.GetObservers(Vector2.Zero)));

        index.SetLocation(client, new Vector2(1024, 1024), 16);

        Assert.Empty(index.GetObservers(Vector2.Zero));
        Assert.Empty(index.GetChunkObservers(Point2.Zero));
        Assert.Same(client, Assert.Single(index.GetObservers(new Vector2(1024, 1024))));
        Assert.Empty(index.GetObservers(new Vector2(1100, 1024)));
    }

    [Fact]
    public void EventRadiusUsesCentersRatherThanContentDistancesAndDoesNotDuplicate()
    {
        var index = new SpatialInterestIndex();
        var client = CreateClient();
        index.SetLocation(client, new Vector2(100, 0), 1);

        Assert.Empty(index.GetObservers(Vector2.Zero));
        Assert.Same(client, Assert.Single(index.GetObserversWithin(Vector2.Zero, 100)));
        Assert.Empty(index.GetObserversWithin(Vector2.Zero, 99));
    }

    [Fact]
    public void DisconnectAndReusedClientNumberDoNotRetainOldConnection()
    {
        var index = new SpatialInterestIndex();
        var oldClient = CreateClient();
        var newClient = CreateClient();
        index.SetLocation(oldClient, Vector2.Zero, 16);
        index.SetLocation(newClient, new Vector2(1024, 0), 16);
        index.RetainClients([newClient]);

        Assert.Empty(index.GetObservers(Vector2.Zero));
        Assert.Empty(index.GetObserversWithin(Vector2.Zero, 16));
        Assert.Same(newClient, Assert.Single(index.GetObservers(new Vector2(1024, 0))));
        index.RetainClients([]);
        Assert.Empty(index.GetObserversWithin(new Vector2(1024, 0), 100));
    }

    [Fact]
    public void LargeRadiusUsesSparseCentersAndIncludesItsExactNegativeBoundary()
    {
        var index = new SpatialInterestIndex();
        var nearby = CreateClient();
        var boundary = CreateClient();
        var outside = CreateClient();
        index.SetLocation(nearby, Vector2.Zero, 0);
        index.SetLocation(boundary, new Vector2(-100_000, 0), 0);
        index.SetLocation(outside, new Vector2(-100_001, 0), 0);

        var observers = index.GetObserversWithin(Vector2.Zero, 100_000).ToArray();

        Assert.Equal(2, observers.Length);
        Assert.Contains(observers, observer => ReferenceEquals(observer, nearby));
        Assert.Contains(observers, observer => ReferenceEquals(observer, boundary));
        Assert.DoesNotContain(observers, observer => ReferenceEquals(observer, outside));
        Assert.Same(nearby, Assert.Single(index.GetObserversWithin(Vector2.Zero, 0)));
    }

    private static Client CreateClient()
    {
        return new Client(null, 1, Guid.NewGuid(), Guid.NewGuid(), null);
    }

    [Fact]
    public void IndexedQueriesMatchFullScanForDeterministicMixedLocations()
    {
        var random = new Random(9137);
        var index = new SpatialInterestIndex();
        var locations = Enumerable.Range(0, 100).Select(_ =>
            (Client: CreateClient(), Center: new Vector2(random.Next(-1024, 1025), random.Next(-1024, 1025)),
                Distance: (float)random.Next(0, 129))).ToArray();
        foreach (var location in locations)
        {
            index.SetLocation(location.Client, location.Center, location.Distance);
        }

        for (var query = 0; query < 200; query++)
        {
            var point = new Vector2(random.Next(-1200, 1201), random.Next(-1200, 1201));
            var chunk = new Point2((int)MathF.Floor(point.X / 16), (int)MathF.Floor(point.Y / 16));
            var radius = random.Next(0, 257);
            var pointExpected = locations.Where(location => Vector2.DistanceSquared(point, location.Center) <=
                location.Distance * location.Distance).Select(location => location.Client);
            var radiusExpected = locations.Where(location => Vector2.DistanceSquared(point, location.Center) <=
                radius * radius).Select(location => location.Client);
            var chunkExpected = locations.Where(location =>
            {
                var nearest = new Vector2(Math.Clamp(location.Center.X, chunk.X * 16f, chunk.X * 16f + 16),
                    Math.Clamp(location.Center.Y, chunk.Y * 16f, chunk.Y * 16f + 16));
                return Vector2.DistanceSquared(nearest, location.Center) <= location.Distance * location.Distance;
            }).Select(location => location.Client);

            Assert.True(new HashSet<Client>(pointExpected, ReferenceEqualityComparer.Instance)
                .SetEquals(index.GetObservers(point)));
            Assert.True(new HashSet<Client>(radiusExpected, ReferenceEqualityComparer.Instance)
                .SetEquals(index.GetObserversWithin(point, radius)));
            Assert.True(new HashSet<Client>(chunkExpected, ReferenceEqualityComparer.Instance)
                .SetEquals(index.GetChunkObservers(chunk)));
        }
    }
}
