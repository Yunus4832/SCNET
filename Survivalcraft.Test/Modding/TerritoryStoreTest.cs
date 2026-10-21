using Engine.Core.Mathematics;

using EntitySystem.TemplatesDatabase;

using Game.Components;

using TerritoryStoneMod;

namespace Survivalcraft.Test.Modding;

public class TerritoryStoreTest
{
    [Fact]
    public void StoreIsWorldLocalAndEnforcesOneStonePerOwner()
    {
        var owner = Guid.NewGuid();
        var first = new TerritoryStore();
        var second = new TerritoryStore();
        Assert.False(first.TryCreate(Guid.Empty, new Point3(0, 64, 0)));
        Assert.True(first.TryCreate(owner, new Point3(0, 64, 0)));
        Assert.False(first.TryCreate(owner, new Point3(128, 64, 128)));
        Assert.True(second.TryCreate(owner, new Point3(128, 64, 128)));
    }

    [Theory]
    [InlineData(7, 9)]
    [InlineData(-1, -17)]
    [InlineData(16, 16)]
    public void BoundsAreCenteredOnStoneAcrossChunkAndNegativeCoordinates(int x, int z)
    {
        var territory = new Territory(Guid.NewGuid(), new Point3(x, 64, z), new TerritorySettings(Size: 4));
        Assert.Equal(x + 0.5f, territory.Bounds.Center().X);
        Assert.Equal(z + 0.5f, territory.Bounds.Center().Z);
        Assert.True(territory.Contains(x - 4, z + 4));
        Assert.False(territory.Contains(x - 5, z));
        Assert.False(territory.Contains(x + 5, z));
        Assert.True(territory.Contains(new Vector3(x + 0.5f, 255, z + 0.5f)));
        Assert.Equal(0, territory.Bounds.Min.Y);
        Assert.Equal(256, territory.Bounds.Max.Y);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void EntryAndExitAreIndependent(bool entry, bool exit)
    {
        var territory = new Territory(
            Guid.NewGuid(),
            new Point3(7, 64, 9),
            new TerritorySettings(Size: 4, AllowEntry: entry, AllowExit: exit)
        );
        Assert.Equal(!exit, territory.BlocksCrossing(new Vector3(7.5f, 64, 9.5f)));
        foreach (var outside in new[]
                 {
                     new Vector3(2.5f, 64, 9.5f), new Vector3(12.5f, 64, 9.5f),
                     new Vector3(7.5f, 64, 4.5f), new Vector3(7.5f, 64, 14.5f)
                 })
        {
            Assert.Equal(!entry, territory.BlocksCrossing(outside));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CollisionWallsPushBackOnAllFourSides(bool exiting)
    {
        var territory = new Territory(Guid.NewGuid(), new Point3(0, 64, 0), new TerritorySettings(Size: 4));
        var walls = territory.GetBoundaryBoxes(60, 70, exiting);
        var min = exiting ? -4f : -3.8f;
        var max = exiting ? 5f : 4.8f;
        var bodies = new[]
        {
            new BoundingBox(new Vector3(min - 0.3f, 63, 0), new Vector3(min + 0.3f, 65, 1)),
            new BoundingBox(new Vector3(max - 0.3f, 63, 0), new Vector3(max + 0.3f, 65, 1)),
            new BoundingBox(new Vector3(0, 63, min - 0.3f), new Vector3(1, 65, min + 0.3f)),
            new BoundingBox(new Vector3(0, 63, max - 0.3f), new Vector3(1, 65, max + 0.3f))
        };
        for (var index = 0; index < 4; index++)
        {
            var correction =
                ComponentBody.CalculateBoxBoxOverlap(ref bodies[index], ref walls[index], index < 2 ? 0 : 2);
            var expectedPositive = (index % 2 == 0) == exiting;
            Assert.True(expectedPositive ? correction > 0 : correction < 0);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BoundaryOverlapOnlyBlocksMovementInTheRestrictedDirection(bool exiting)
    {
        var territory = new Territory(Guid.NewGuid(), new Point3(0, 64, 0), new TerritorySettings(Size: 4));
        var normals = new[] { -Vector3.UnitX, Vector3.UnitX, -Vector3.UnitZ, Vector3.UnitZ };
        var edges = new[]
        {
            new Vector3(territory.MinX, 64, 0), new Vector3(territory.MaxX, 64, 0),
            new Vector3(0, 64, territory.MinZ), new Vector3(0, 64, territory.MaxZ)
        };
        for (var face = 0; face < edges.Length; face++)
        {
            var position = edges[face] + normals[face] * (exiting ? -0.1f : 0.1f);
            var body = new BoundingBox(
                position - new Vector3(0.3f, 0, 0.3f),
                position + new Vector3(0.3f, 1.8f, 0.3f)
            );
            var restricted = normals[face] * (exiting ? 0.1f : -0.1f);
            Assert.Single(
                territory.GetBlockingBoundaryBoxes(63, 67, restricted, exiting),
                wall => wall.Intersection(body)
            );
            Assert.DoesNotContain(
                territory.GetBlockingBoundaryBoxes(63, 67, -restricted, exiting),
                wall => wall.Intersection(body)
            );
            var tangent = face < 2 ? Vector3.UnitZ : Vector3.UnitX;
            Assert.DoesNotContain(
                territory.GetBlockingBoundaryBoxes(63, 67, tangent, exiting),
                wall => wall.Intersection(body)
            );
            Assert.DoesNotContain(
                territory.GetBlockingBoundaryBoxes(63, 67, Vector3.UnitY, exiting),
                wall => wall.Intersection(body)
            );
        }
    }

    [Theory]
    [InlineData(4)]
    [InlineData(32)]
    public void RadiusLimitsAreAcceptedByAuthoritativeSettings(int radius)
    {
        var store = new TerritoryStore();
        var owner = Guid.NewGuid();
        Assert.True(store.TryCreate(owner, new Point3(0, 64, 0)));
        Assert.True(store.TryChangeSettings(owner, new TerritorySettings(Size: radius), out _));
        Assert.Equal(radius * 2 + 1, Assert.Single(store.Territories).MaxX - Assert.Single(store.Territories).MinX);
    }

    [Fact]
    public void ResizeRejectsOverlapAtomicallyAndAllowsAdjacentTerritories()
    {
        var store = new TerritoryStore();
        var owner = Guid.NewGuid();
        Assert.True(store.TryCreate(owner, new Point3(0, 64, 0)));
        Assert.False(store.TryCreate(Guid.NewGuid(), new Point3(32, 200, 0)));
        Assert.True(store.TryCreate(Guid.NewGuid(), new Point3(33, 200, 0)));
        Assert.True(store.TryGet(owner, out var territory));
        var original = territory!.Settings;
        Assert.False(store.TryChangeSettings(owner, original with { Size = 17, AllowEntry = true }, out var error));
        Assert.Equal("Overlaps", error);
        Assert.Equal(original, territory.Settings);
        Assert.True(store.TryChangeSettings(owner, original with { Size = 4 }, out _));
        Assert.Equal(4, territory.Settings.Size);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(33)]
    [InlineData(int.MaxValue)]
    public void ResizeRejectsInvalidRadius(int size)
    {
        var store = new TerritoryStore();
        var owner = Guid.NewGuid();
        Assert.True(store.TryCreate(owner, new Point3(0, 64, 0)));
        Assert.False(store.TryChangeSettings(owner, new TerritorySettings(Size: size), out var error));
        Assert.Equal("InvalidSize", error);
    }

    [Fact]
    public void SnapshotsValidateTheActualRadiusNotTheDefaultRadius()
    {
        var store = new TerritoryStore();
        var owner = Guid.NewGuid();
        store.ApplySnapshot(
            [
                new Territory(owner, new Point3(0, 64, 0), new TerritorySettings(Size: 4)),
                new Territory(Guid.NewGuid(), new Point3(9, 200, 0), new TerritorySettings(Size: 4))
            ]
        );
        Assert.Equal(2, store.Territories.Count);
        Assert.Throws<InvalidDataException>(() => store.ApplyUpdate(
                new Territory(owner, new Point3(0, 64, 0), new TerritorySettings(Size: 5))
            )
        );
        Assert.Equal(4, store.Territories.First(item => item.Owner == owner).Settings.Size);
    }

    [Fact]
    public void SaveLoadPreservesAllSettingsAndRemovalRequiresActualStone()
    {
        var source = new TerritoryStore();
        var owner = Guid.NewGuid();
        var point = new Point3(-1, 65, -17);
        Assert.True(source.TryCreate(owner, point));
        var settings = new TerritorySettings(7, true, false, true, false, false);
        Assert.True(source.TryChangeSettings(owner, settings, out _));
        var restored = new TerritoryStore();
        restored.Load(source.Save());
        var loaded = Assert.Single(restored.Territories);
        Assert.Equal(point, loaded.StonePoint);
        Assert.Equal(settings, loaded.Settings);
        Assert.False(restored.RemoveStone(new Point3(point.X, point.Y + 1, point.Z)));
        Assert.True(restored.RemoveStone(point));
    }

    [Fact]
    public void InvalidSavedTerritoriesDoNotPartiallyReplaceLiveState()
    {
        var store = new TerritoryStore();
        var owner = Guid.NewGuid();
        Assert.True(store.TryCreate(owner, new Point3(128, 64, 128)));
        var values = store.Save();
        ((ValuesDictionary)values.Single().Value).SetValue("Size", 0);
        Assert.Throws<InvalidDataException>(() => store.Load(values));
        Assert.Equal(owner, Assert.Single(store.Territories).Owner);
    }
}
