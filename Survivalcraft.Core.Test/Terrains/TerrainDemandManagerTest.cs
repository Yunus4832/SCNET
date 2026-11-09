using Engine.Core.Mathematics;

using Game.Protocol.Terrain;
using Game.Terrains;
using Game.Terrains.Distribution;

namespace Survivalcraft.Core.Test.Terrains;

public sealed class TerrainDemandManagerTest
{
    [Fact]
    public void ApprovedLocationCorrectionsWithinEightMetersAreNeverSwallowed()
    {
        using var terrain = new Terrain();
        using var demands = new TerrainDemandManager(terrain, TerrainContentRole.Authority);
        demands.SetLocation(0, new Vector2(72, 0), 64, 64);
        var first = demands.CapturePending();
        demands.Apply(first);
        demands.Reconciled(first);
        Assert.True(demands.SetLocation(0, new Vector2(80, 0), 64, 64));
        Assert.Equal(new Vector2(80, 0), demands.GetRequestedLocation(0).Center);
        Assert.False(demands.SetLocation(0, new Vector2(80, 0), 64, 64));
        Assert.True(demands.HasChanges);
    }

    [Fact]
    public void NewDemandDuringResidencyReconciliationCannotBeClearedByAnOldSnapshot()
    {
        using var terrain = new Terrain();
        using var demands = new TerrainDemandManager(terrain, TerrainContentRole.Authority);
        demands.SetLocation(0, Vector2.Zero, 64, 64);
        var old = demands.CapturePending();
        demands.Apply(old);
        demands.SetLocation(0, new Vector2(128, 0), 64, 64);
        demands.Reconciled(old);
        Assert.True(demands.HasChanges);
        Assert.Equal(Vector2.Zero, demands.Locations[0].Center);
        Assert.True(demands.IsLocationCovered(0, new Vector2(128, 0), 32));
        var current = demands.CapturePending();
        demands.Apply(current);
        demands.Reconciled(current);
        Assert.False(demands.HasChanges);
        Assert.Equal(new Vector2(128, 0), demands.Locations[0].Center);
    }

    [Fact]
    public void BackpressureKeepsDemandAndPreparationUntilItIsReconciled()
    {
        using var terrain = new Terrain();
        using var demands = new TerrainDemandManager(terrain, TerrainContentRole.Authority);
        Assert.Empty(demands.CaptureEffectiveLocations());
        Assert.False(demands.HasChanges);
        demands.SetLocation(0, Vector2.Zero, 128, 128);
        using var preparation = demands.BeginSpawnPreparation(Guid.NewGuid(), new Vector2(512, 0), 64);
        var snapshot = demands.CapturePending();
        demands.Apply(snapshot);
        Assert.True(demands.HasChanges);
        Assert.Equal(2, demands.CaptureEffectiveLocations().Length);
        preparation.Dispose();
        demands.Reconciled(snapshot);
        Assert.True(demands.HasChanges);
        Assert.Single(demands.CaptureEffectiveLocations());
        demands.RemoveLocation(0);
        Assert.False(demands.IsLocationCovered(0, Vector2.Zero, 1));
        Assert.Empty(demands.CaptureEffectiveLocations());
    }

    [Fact]
    public void SpawnProgressSeparatesPublishedContentsFromLighting()
    {
        using var terrain = new Terrain();
        using var demands = new TerrainDemandManager(terrain, TerrainContentRole.Authority);
        using var preparation = demands.BeginSpawnPreparation(Guid.NewGuid(), new Vector2(8, 8), 32);
        var chunk = terrain.AllocateChunk(0, 0);
        Assert.Equal(0, preparation.GetProgress(0, 16));
        chunk.HasContents = true;
        Assert.Equal(1, preparation.GetProgress(0, 16));
        Assert.Equal(0, preparation.GetProgress(16, 0));
        var request = chunk.DerivedRevisions.Capture();
        chunk.DerivedRevisions.PublishHeights(request);
        chunk.DerivedRevisions.PublishSunlight(request);
        chunk.DerivedRevisions.PublishLight(request);
        Assert.Equal(1, preparation.GetProgress(16, 0));
    }

    [Fact]
    public void ReplicaCannotCreateAuthoritySpawnDemandAndDoesNotOwnAnotherWorldsLease()
    {
        using var terrain = new Terrain();
        using var replica = new TerrainDemandManager(terrain, TerrainContentRole.Replica);
        Assert.Throws<InvalidOperationException>(() => replica.BeginSpawnPreparation(Guid.NewGuid(), Vector2.Zero, 32));
        using var first = new TerrainDemandManager(terrain, TerrainContentRole.Authority);
        using var next = new TerrainDemandManager(terrain, TerrainContentRole.Authority);
        var id = Guid.NewGuid();
        using var old = first.BeginSpawnPreparation(id, Vector2.Zero, 32);
        using var current = next.BeginSpawnPreparation(id, Vector2.One, 32);
        first.Dispose();
        Assert.Throws<ObjectDisposedException>(() => old.GetProgress(0, 16));
        old.Dispose();
        Assert.Single(next.CaptureEffectiveLocations());
    }

    [Fact]
    public void QueuedRemovalDoesNotReuseAnAppliedTeleportLocation()
    {
        var applied = new TerrainUpdateLocation
        {
            Center = new Vector2(100f, 100f),
            ContentDistance = 16f,
            LastChunksUpdateCenter = new Vector2(100f, 100f)
        };
        var current = new Dictionary<int, TerrainUpdateLocation> { [-2] = applied };
        var pending = new Dictionary<int, TerrainUpdateLocation?> { [-2] = null };
        Assert.Equal(default, TerrainDemandManager.ResolveLocation(-2, current, pending));
        pending.Clear();
        Assert.Equal(applied, TerrainDemandManager.ResolveLocation(-2, current, pending));
    }

    [Fact]
    public void LatestQueuedLocationWinsBeforeWorkerHandoff()
    {
        var latest = new TerrainUpdateLocation { Center = new Vector2(200f, 200f) };
        var current = new Dictionary<int, TerrainUpdateLocation> { [-2] = new() { Center = Vector2.Zero } };
        var pending = new Dictionary<int, TerrainUpdateLocation?> { [-2] = latest };
        Assert.Equal(latest, TerrainDemandManager.ResolveLocation(-2, current, pending));
    }
}
