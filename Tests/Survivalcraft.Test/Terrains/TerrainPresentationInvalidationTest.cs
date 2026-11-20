using System.Reflection;

using Game.Subsystems;
using Game.Terrains;
using Game.Terrains.Rendering;

namespace Survivalcraft.Test.Terrains;

public sealed class TerrainPresentationInvalidationTest
{
    [Fact]
    public void DerivedInputNotificationInvalidatesMeshWithoutReenteringTheScheduler()
    {
        using var terrain = new Terrain();
        using var presentations = new TerrainChunkPresentationStore();
        var chunk = terrain.AllocateChunk(0, 0);
        var mesh = presentations.Get(chunk);
        TerrainMeshTestData.Complete(chunk, mesh);
        Assert.True(mesh.HasCurrentGeometry(chunk));
        var subsystem = new SubsystemTerrainPresentation();
        typeof(SubsystemTerrainPresentation).GetProperty(nameof(subsystem.ChunkPresentations))!
            .SetValue(subsystem, presentations);
        var notify = typeof(SubsystemTerrainPresentation).GetMethod(
            "OnDerivedInputsInvalidated",
            BindingFlags.Instance | BindingFlags.NonPublic
        )!;

        notify.Invoke(subsystem, [chunk]);

        Assert.False(mesh.HasCurrentGeometry(chunk));
        TerrainMeshTestData.Complete(chunk, mesh);
        Assert.True(mesh.HasCurrentGeometry(chunk));
        notify.Invoke(subsystem, [chunk]);
        Assert.False(mesh.HasCurrentGeometry(chunk));
    }
}
