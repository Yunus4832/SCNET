using Engine.Core.Mathematics;

using Game.Terrains;
using Game.Terrains.Rendering;

namespace Survivalcraft.Test.Terrains;

public sealed class TerrainEditCohortsTest
{
    [Fact]
    public void InteriorEditIsUrgentWithoutCreatingAnUploadCohort()
    {
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        chunk.HasContents = true;
        var urgent = new HashSet<TerrainChunk>();
        var cohorts = new TerrainEditCohorts(
            terrain,
            (c, value) =>
            {
                if (value) { urgent.Add(c); }
                else { urgent.Remove(c); }
            }
        );
        cohorts.Track(new Point3(8, 64, 8));
        Assert.Contains(chunk, urgent);
        Assert.Null(cohorts.Find(chunk));
    }

    [Fact]
    public void CornerTracksFaceNeighborsWithoutWaitingForDiagonalLighting()
    {
        using var terrain = new Terrain();
        for (var x = -1; x <= 1; x++)
        {
            for (var z = -1; z <= 1; z++) { terrain.AllocateChunk(x, z).HasContents = true; }
        }

        var urgent = new HashSet<TerrainChunk>();
        var cohorts = new TerrainEditCohorts(
            terrain,
            (c, value) =>
            {
                if (value) { urgent.Add(c); }
                else { urgent.Remove(c); }
            }
        );
        cohorts.Track(new Point3(0, 64, 0));
        var group = Assert.Single(cohorts.Groups);
        Assert.Equal(3, group.Count);
        Assert.All(group, c => Assert.True(c.Coords.X is -1 or 0 && c.Coords.Y is -1 or 0));
        Assert.Equal(3, urgent.Count);
        Assert.DoesNotContain(terrain.GetChunkAtCoords(-1, -1)!, group);
        cohorts.Complete(group);
        Assert.Empty(urgent);
        Assert.Empty(cohorts.Groups);
    }

    [Fact]
    public void OverlappingEditsMergeAndRetiredMembersLeaveTheCohort()
    {
        using var terrain = new Terrain();
        var chunks = Enumerable.Range(0, 3).Select(x => terrain.AllocateChunk(x, 0)).ToArray();
        foreach (var chunk in chunks) { chunk.HasContents = true; }

        var urgent = new HashSet<TerrainChunk>();
        var cohorts = new TerrainEditCohorts(
            terrain,
            (c, value) =>
            {
                if (value) { urgent.Add(c); }
                else { urgent.Remove(c); }
            }
        );
        cohorts.Track(new Point3(15, 64, 8));
        cohorts.Track(new Point3(31, 64, 8));
        var group = Assert.Single(cohorts.Groups);
        Assert.Equal(3, group.Count);
        Assert.All(chunks, chunk => Assert.Same(group, cohorts.Find(chunk)));
        cohorts.Release(chunks[0]);
        Assert.DoesNotContain(chunks[0], group);
        Assert.DoesNotContain(chunks[0], urgent);
        Assert.Equal(2, group.Count);
        cohorts.Clear();
        Assert.Empty(urgent);
    }
}
