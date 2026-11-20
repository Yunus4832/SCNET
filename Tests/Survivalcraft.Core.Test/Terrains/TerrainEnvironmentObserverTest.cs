using Game.Subsystems;
using Game.Terrains;
using Game.World;
using Game.World.Timing;

namespace Survivalcraft.Core.Test.Terrains;

public sealed class TerrainEnvironmentObserverTest
{
    [Theory]
    [InlineData(0f, 0)]
    [InlineData(0.5f, -12)]
    [InlineData(0.625f, -24)]
    [InlineData(1f, 0)]
    public void SeasonSamplingPreservesTheCurveAndDoesNotInvalidateCpuInputs(float season, int temperature)
    {
        var clock = new WorldUpdateClock();
        clock.Advance(.02f, 1);
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        var request = chunk.DerivedRevisions.Capture();
        terrain.SeasonTemperature = 100;
        terrain.SeasonHumidity = 100;
        var info = new SubsystemGameInfo { WorldSettings = new WorldSettings { TimeOfYear = season } };
        var sky = new SubsystemSky { SkyLightValue = 15 };
        var observer = new TerrainEnvironmentObserver(terrain, info, clock);

        Assert.True(observer.Update());
        Assert.Equal(temperature, terrain.SeasonTemperature);
        Assert.Equal(0, terrain.SeasonHumidity);
        Assert.Equal(request, chunk.DerivedRevisions.Capture());
        Assert.False(observer.Update());

        sky.SkyLightValue = 7;
        info.WorldSettings.TimeOfYear = .625f;
        clock.Advance(.02f, 1.5);
        Assert.False(observer.Update());
        Assert.Equal(temperature, terrain.SeasonTemperature);
    }
}
