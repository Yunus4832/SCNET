using Engine.Core;

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
        var firstWorld = new TerritoryStore();
        var secondWorld = new TerritoryStore();

        Assert.False(firstWorld.TryCreate(Guid.Empty, new Point3(0, 64, 0)));
        Assert.True(firstWorld.TryCreate(owner, new Point3(0, 64, 0)));
        Assert.False(firstWorld.TryCreate(owner, new Point3(128, 64, 128)));
        Assert.True(secondWorld.TryCreate(owner, new Point3(128, 64, 128)));
        Assert.Single(firstWorld.Territories);
        Assert.Single(secondWorld.Territories);
    }

    [Fact]
    public void RemovalRequiresTheActualStoneNotAnotherCellInsideItsArea()
    {
        var store = new TerritoryStore();
        var stone = new Point3(3, 64, 5);
        Assert.True(store.TryCreate(Guid.NewGuid(), stone));

        Assert.False(store.RemoveStone(new Point3(4, 64, 5)));
        Assert.False(store.RemoveStone(new Point3(3, 65, 5)));
        Assert.Single(store.Territories);
        Assert.True(store.RemoveStone(stone));
        Assert.Empty(store.Territories);
    }

    [Fact]
    public void SharedBoundaryCountsAsOverlapRegardlessOfStoneHeight()
    {
        var store = new TerritoryStore();
        Assert.True(store.TryCreate(Guid.NewGuid(), new Point3(0, 64, 0)));
        Assert.False(store.TryCreate(Guid.NewGuid(), new Point3(32, 200, 0)));
        Assert.True(store.TryCreate(Guid.NewGuid(), new Point3(48, 200, 0)));
    }

    [Fact]
    public void BoundaryBoxesCoverAllFourSidesOfTheStoneChunk()
    {
        var territory = new Territory(Guid.NewGuid(), new Point3(7, 64, 9));
        var boxes = territory.GetBoundaryBoxes(60, 70);

        Assert.Equal(4, boxes.Length);
        Assert.Contains(boxes, box => box.Intersection(new BoundingBox(
            new Vector3(-15.8f, 63, 0), new Vector3(-15.2f, 65, 1))));
        Assert.Contains(boxes, box => box.Intersection(new BoundingBox(
            new Vector3(16.2f, 63, 0), new Vector3(16.8f, 65, 1))));
        Assert.Contains(boxes, box => box.Intersection(new BoundingBox(
            new Vector3(0, 63, -15.8f), new Vector3(1, 65, -15.2f))));
        Assert.Contains(boxes, box => box.Intersection(new BoundingBox(
            new Vector3(0, 63, 16.2f), new Vector3(1, 65, 16.8f))));

        var west = new BoundingBox(new Vector3(-15.2f, 63, 0), new Vector3(-14.6f, 65, 1));
        var east = new BoundingBox(new Vector3(15.6f, 63, 0), new Vector3(16.2f, 65, 1));
        var north = new BoundingBox(new Vector3(0, 63, -15.2f), new Vector3(1, 65, -14.6f));
        var south = new BoundingBox(new Vector3(0, 63, 15.6f), new Vector3(1, 65, 16.2f));
        Assert.True(ComponentBody.CalculateBoxBoxOverlap(ref west, ref boxes[0], 0) > 0);
        Assert.True(ComponentBody.CalculateBoxBoxOverlap(ref east, ref boxes[1], 0) < 0);
        Assert.True(ComponentBody.CalculateBoxBoxOverlap(ref north, ref boxes[2], 2) > 0);
        Assert.True(ComponentBody.CalculateBoxBoxOverlap(ref south, ref boxes[3], 2) < 0);

        Assert.True(territory.Contains(new Vector3(0.5f, 64, 0.5f)));
        Assert.False(territory.Contains(new Vector3(-17.5f, 64, 0.5f)));
        Assert.False(territory.Contains(new Vector3(17.5f, 64, 0.5f)));
        Assert.False(territory.Contains(new Vector3(0.5f, 64, -17.5f)));
        Assert.False(territory.Contains(new Vector3(0.5f, 64, 17.5f)));
    }

    [Fact]
    public void RoundTripPreservesSettingsAndLoadReplacesPreviousWorldState()
    {
        var owner = Guid.NewGuid();
        var point = new Point3(-1, 65, -17);
        var source = new TerritoryStore();
        Assert.True(source.TryCreate(owner, point));
        Assert.True(source.TryGet(owner, out var territory));
        territory!.ApplyToTeam = true;
        territory.ShowBoundary = false;
        territory.RestrictEntry = false;

        var restored = new TerritoryStore();
        Assert.True(restored.TryCreate(Guid.NewGuid(), new Point3(128, 64, 128)));
        restored.Load(source.Save());

        var loaded = Assert.Single(restored.Territories);
        Assert.Equal(owner, loaded.Owner);
        Assert.Equal(point, loaded.StonePoint);
        Assert.Equal(new Point2(-16, -32), loaded.Origin);
        Assert.True(loaded.ApplyToTeam);
        Assert.False(loaded.ShowBoundary);
        Assert.False(loaded.RestrictEntry);
        Assert.Same(loaded, restored.Find(-16, -32));
    }

    [Fact]
    public void InvalidSavedTerritoriesDoNotPartiallyReplaceLiveState()
    {
        var store = new TerritoryStore();
        var owner = Guid.NewGuid();
        Assert.True(store.TryCreate(owner, new Point3(128, 64, 128)));
        var invalid = new TerritoryStore();
        Assert.True(invalid.TryCreate(Guid.NewGuid(), new Point3(0, 64, 0)));
        Assert.True(invalid.TryCreate(Guid.NewGuid(), new Point3(48, 64, 0)));
        var values = invalid.Save();
        var data = (ValuesDictionary)values.Last().Value;
        data.SetValue("StonePoint", new Point3(16, 64, 0));

        Assert.Throws<InvalidDataException>(() => store.Load(values));
        Assert.Equal(owner, Assert.Single(store.Territories).Owner);
    }
}
