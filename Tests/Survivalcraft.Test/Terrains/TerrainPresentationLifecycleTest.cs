using Game.Subsystems;
using Game.Terrains;

namespace Survivalcraft.Test.Terrains;

public sealed class TerrainPresentationLifecycleTest
{
    [Fact]
    public void TerrainNotifiesEveryOwnerBeforeReleasingChunksEvenWhenAnOwnerFails()
    {
        var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        var subsystem = new SubsystemTerrain { Terrain = terrain };
        var called = new List<string>();
        subsystem.Closing += _ =>
        {
            Assert.False(chunk.IsDisposed);
            called.Add("first");
            throw new InvalidOperationException("Owner cleanup failed.");
        };
        subsystem.Closing += _ =>
        {
            Assert.False(chunk.IsDisposed);
            called.Add("second");
        };

        var error = Assert.Throws<AggregateException>(subsystem.Dispose);
        subsystem.Dispose();

        Assert.Single(error.InnerExceptions);
        Assert.Equal(new[] { "first", "second" }, called);
        Assert.True(chunk.IsDisposed);
    }

    [Fact]
    public void UnloadedPresentationCanBeDisposedRepeatedly()
    {
        var presentation = new SubsystemTerrainPresentation();
        presentation.Dispose();
        presentation.Dispose();
    }
}
