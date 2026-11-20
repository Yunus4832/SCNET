using System.Reflection;
using System.Runtime.CompilerServices;

using Engine.Core.Mathematics;

using Game.Characters;
using Game.Components;
using Game.Subsystems;
using Game.Terrains;

namespace Survivalcraft.Test.Terrains;

public sealed class TerrainSpawnPreparationTest
{
    [Fact]
    public void RespawnPreparationRetainsApprovedPeerOuterRingAndReleasesOnlyItsOwnInterest()
    {
        using var fixture = new DemandFixture();
        var peer = new TerrainUpdateLocation { Center = Vector2.Zero, VisibilityDistance = 128, ContentDistance = 128 };
        fixture.ApplyLocation(0, peer);
        peer = fixture.Demands.Locations[0];
        var outer = fixture.Terrain.AllocateChunk(5, 0);
        using var preparation = fixture.Demands.BeginSpawnPreparation(Guid.NewGuid(), Vector2.Zero, 64);
        var effective = fixture.Demands.CaptureEffectiveLocations();

        Assert.Equal(peer, fixture.Demands.Locations[0]);
        Assert.Equal(2, effective.Length);
        Assert.False(TerrainResidencyManager.ShouldDiscardChunk(outer, effective, 0, new HashSet<Point2>()));
        Assert.True(
            TerrainResidencyManager.ShouldDiscardChunk(
                outer,
                [new TerrainUpdateLocation { Center = Vector2.Zero, ContentDistance = 64 }],
                0,
                new HashSet<Point2>()
            )
        );
        preparation.Dispose();
        Assert.Equal(peer, Assert.Single(fixture.Demands.CaptureEffectiveLocations()));
        Assert.Same(outer, fixture.Terrain.GetChunkAtCoords(5, 0));
        Assert.Throws<ObjectDisposedException>(() => preparation.Update(Vector2.One, 64));
        Assert.Throws<ObjectDisposedException>(() => preparation.GetProgress(32, 0));
    }

    [Fact]
    public void PreparationAloneRetainsItsRequiredChunksWithoutCreatingPeerInterest()
    {
        using var fixture = new DemandFixture();
        var chunk = fixture.Terrain.AllocateChunk(0, 0);
        using var preparation = fixture.Demands.BeginSpawnPreparation(Guid.NewGuid(), Vector2.Zero, 64);
        Assert.Empty(fixture.Demands.Locations);
        var effective = fixture.Demands.CaptureEffectiveLocations();
        Assert.Single(effective);
        Assert.True(TerrainResidencyManager.IsChunkInRange(chunk.Center, effective));
        Assert.False(TerrainResidencyManager.ShouldDiscardChunk(chunk, effective, 0, new HashSet<Point2>()));
    }

    [Fact]
    public void HandoffRequiresPeerCoverageAndRecognizesThePendingApprovedLocation()
    {
        using var fixture = new DemandFixture();
        using var preparation = fixture.Demands.BeginSpawnPreparation(Guid.NewGuid(), new Vector2(256, 0), 64);
        Assert.False(fixture.Demands.IsLocationCovered(0, new Vector2(256, 0), 32));
        fixture.ApplyLocation(0, new TerrainUpdateLocation { Center = Vector2.Zero, ContentDistance = 128 });
        Assert.False(fixture.Demands.IsLocationCovered(0, new Vector2(256, 0), 32));
        fixture.Demands.SetLocation(0, new Vector2(256, 0), 128, 128);
        Assert.True(fixture.Demands.IsLocationCovered(0, new Vector2(256, 0), 32));
        preparation.Dispose();
        Assert.Single(fixture.Demands.CaptureEffectiveLocations());
    }

    [Fact]
    public void ClosedTerrainInvalidatesItsLeaseWithoutTouchingTheNextWorld()
    {
        using var first = new DemandFixture();
        var characterId = Guid.NewGuid();
        using var old = first.Demands.BeginSpawnPreparation(characterId, Vector2.Zero, 64);
        first.Demands.Dispose();
        using var second = new DemandFixture();
        using var current = second.Demands.BeginSpawnPreparation(characterId, Vector2.Zero, 64);
        Assert.Throws<ObjectDisposedException>(() => old.Update(Vector2.One, 64));
        Assert.Throws<ObjectDisposedException>(() => old.GetProgress(32, 0));
        old.Dispose();
        Assert.Single(second.Demands.CaptureEffectiveLocations());
    }

    [Fact]
    public void PlayingHandoffHasAnExplicitBoundedDeadlineWithoutFabricatingPeerInterest()
    {
        using var fixture = new DemandFixture();
        using var preparation = fixture.Demands.BeginSpawnPreparation(Guid.NewGuid(), Vector2.Zero, 64);
        var character = (CharacterData)RuntimeHelpers.GetUninitializedObject(typeof(CharacterData));
        character.CharacterId = preparation.CharacterId;
        character.ComponentCharacter = new ComponentCharacter { ComponentBody = new ComponentBody() };
        typeof(CharacterData).GetField("_subsystemTerrain", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(character, fixture.Subsystem);
        typeof(CharacterData).GetField("_spawnPreparation", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(character, preparation);
        character.TryReleaseSpawnPreparation(100);
        Assert.Single(fixture.Demands.CaptureEffectiveLocations());
        character.TryReleaseSpawnPreparation(114.99);
        Assert.Single(fixture.Demands.CaptureEffectiveLocations());
        character.TryReleaseSpawnPreparation(115);
        Assert.Empty(fixture.Demands.CaptureEffectiveLocations());
        Assert.Empty(fixture.Demands.Locations);
        Assert.Throws<ObjectDisposedException>(() => preparation.Update(Vector2.Zero, 64));
    }

    private sealed class DemandFixture : IDisposable
    {
        public Terrain Terrain { get; } = new();

        public TerrainDemandManager Demands { get; }

        public SubsystemTerrain Subsystem { get; }

        public DemandFixture()
        {
            Demands = new TerrainDemandManager(Terrain, Game.Terrains.Distribution.TerrainContentRole.Authority);
            Subsystem = new SubsystemTerrain { Terrain = Terrain };
            typeof(SubsystemTerrain).GetProperty(nameof(SubsystemTerrain.Demands))!.SetValue(Subsystem, Demands);
        }

        public void ApplyLocation(int index, TerrainUpdateLocation location)
        {
            Demands.SetLocation(index, location.Center, location.VisibilityDistance, location.ContentDistance);
            var snapshot = Demands.CapturePending();
            Demands.Apply(snapshot);
            Demands.Reconciled(snapshot);
        }

        public void Dispose()
        {
            Demands.Dispose();
            Subsystem.WorkExecutor.Dispose();
            Terrain.Dispose();
        }
    }
}
