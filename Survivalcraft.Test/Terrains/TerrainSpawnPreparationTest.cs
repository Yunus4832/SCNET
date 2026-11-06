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
        using var fixture = new UpdaterFixture();
        var peer = new TerrainUpdateLocation { Center = Vector2.Zero, VisibilityDistance = 128, ContentDistance = 128 };
        fixture.Locations.Add(0, peer);
        var outer = fixture.Terrain.AllocateChunk(5, 0);
        using var preparation = fixture.Updater.BeginSpawnPreparation(Guid.NewGuid(), Vector2.Zero, 64);
        var effective = fixture.Updater.CaptureEffectiveLocations();

        Assert.Equal(peer, fixture.Updater.UpdateLocations[0]);
        Assert.Equal(2, effective.Length);
        Assert.False(TerrainUpdater.ShouldDiscardChunk(outer, effective, 0, new HashSet<Point2>()));
        Assert.True(
            TerrainUpdater.ShouldDiscardChunk(
                outer,
                [new TerrainUpdateLocation { Center = Vector2.Zero, ContentDistance = 64 }],
                0,
                new HashSet<Point2>()
            )
        );
        preparation.Dispose();
        Assert.Equal(peer, Assert.Single(fixture.Updater.CaptureEffectiveLocations()));
        Assert.Same(outer, fixture.Terrain.GetChunkAtCoords(5, 0));
        Assert.Throws<ObjectDisposedException>(() => preparation.Update(Vector2.One, 64));
        Assert.Throws<ObjectDisposedException>(() => preparation.GetProgress(32, 0));
    }

    [Fact]
    public void PreparationAloneParticipatesInCpuSchedulingWithoutCreatingPeerInterest()
    {
        using var fixture = new UpdaterFixture();
        var chunk = fixture.Terrain.AllocateChunk(0, 0);
        using var preparation = fixture.Updater.BeginSpawnPreparation(Guid.NewGuid(), Vector2.Zero, 64);
        Assert.Empty(fixture.Updater.UpdateLocations);
        var selected = TerrainUpdater.SelectChunkToUpdate(
            [chunk],
            fixture.Updater.CaptureEffectiveLocations(),
            Game.Terrains.Distribution.TerrainContentRole.Authority,
            true,
            out _
        );
        Assert.Same(chunk, selected);
    }

    [Fact]
    public void HandoffRequiresPeerCoverageAndRecognizesThePendingApprovedLocation()
    {
        using var fixture = new UpdaterFixture();
        using var preparation = fixture.Updater.BeginSpawnPreparation(Guid.NewGuid(), new Vector2(256, 0), 64);
        Assert.False(fixture.Updater.IsUpdateLocationCovered(0, new Vector2(256, 0), 32));
        fixture.Locations.Add(0, new TerrainUpdateLocation { Center = Vector2.Zero, ContentDistance = 128 });
        Assert.False(fixture.Updater.IsUpdateLocationCovered(0, new Vector2(256, 0), 32));
        fixture.Updater.SetUpdateLocation(0, new Vector2(256, 0), 128, 128);
        Assert.True(fixture.Updater.IsUpdateLocationCovered(0, new Vector2(256, 0), 32));
        preparation.Dispose();
        Assert.Single(fixture.Updater.CaptureEffectiveLocations());
    }

    [Fact]
    public void ClosedTerrainInvalidatesItsLeaseWithoutTouchingTheNextWorld()
    {
        using var first = new UpdaterFixture();
        var characterId = Guid.NewGuid();
        using var old = first.Updater.BeginSpawnPreparation(characterId, Vector2.Zero, 64);
        first.Updater.Dispose();
        using var second = new UpdaterFixture();
        using var current = second.Updater.BeginSpawnPreparation(characterId, Vector2.Zero, 64);
        Assert.Throws<ObjectDisposedException>(() => old.Update(Vector2.One, 64));
        Assert.Throws<ObjectDisposedException>(() => old.GetProgress(32, 0));
        old.Dispose();
        Assert.Single(second.Updater.CaptureEffectiveLocations());
    }

    [Fact]
    public void PlayingHandoffHasAnExplicitBoundedDeadlineWithoutFabricatingPeerInterest()
    {
        using var fixture = new UpdaterFixture();
        using var preparation = fixture.Updater.BeginSpawnPreparation(Guid.NewGuid(), Vector2.Zero, 64);
        var character = (CharacterData)RuntimeHelpers.GetUninitializedObject(typeof(CharacterData));
        character.CharacterId = preparation.CharacterId;
        character.ComponentCharacter = new ComponentCharacter { ComponentBody = new ComponentBody() };
        typeof(CharacterData).GetField("_subsystemTerrain", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(character, new SubsystemTerrain { TerrainUpdater = fixture.Updater });
        typeof(CharacterData).GetField("_spawnPreparation", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(character, preparation);
        character.TryReleaseSpawnPreparation(100);
        Assert.Single(fixture.Updater.CaptureEffectiveLocations());
        character.TryReleaseSpawnPreparation(114.99);
        Assert.Single(fixture.Updater.CaptureEffectiveLocations());
        character.TryReleaseSpawnPreparation(115);
        Assert.Empty(fixture.Updater.CaptureEffectiveLocations());
        Assert.Empty(fixture.Updater.UpdateLocations);
        Assert.Throws<ObjectDisposedException>(() => preparation.Update(Vector2.Zero, 64));
    }

    private sealed class UpdaterFixture : IDisposable
    {
        public Terrain Terrain { get; } = new();

        public TerrainUpdater Updater { get; } =
            (TerrainUpdater)RuntimeHelpers.GetUninitializedObject(typeof(TerrainUpdater));

        public Dictionary<int, TerrainUpdateLocation> Locations { get; } = new();

        public UpdaterFixture()
        {
            Set("_terrain", Terrain);
            Set("_subsystemTerrain", new SubsystemTerrain { Terrain = Terrain });
            Set("_spawnPreparations", new Dictionary<Guid, TerrainSpawnPreparation>());
            Set("_pendingLocations", new Dictionary<int, TerrainUpdateLocation?>());
            Set("_unpauseLock", Activator.CreateInstance(typeof(Lock))!);
            Set("_pauseEvent", new ManualResetEvent(true));
            Set("<UpdateEvent>k__BackingField", new AutoResetEvent(true));
            Set("_task", Task.CompletedTask);
            var parametersField = typeof(TerrainUpdater).GetField(
                "_updateParameters",
                BindingFlags.Instance | BindingFlags.NonPublic
            )!;
            var parameters = Activator.CreateInstance(parametersField.FieldType)!;
            parametersField.FieldType.GetField("Locations")!.SetValue(parameters, Locations);
            parametersField.SetValue(Updater, parameters);
        }

        private void Set(string name, object value) => typeof(TerrainUpdater)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(Updater, value);

        public void Dispose()
        {
            Updater.Dispose();
            Terrain.Dispose();
        }
    }
}
