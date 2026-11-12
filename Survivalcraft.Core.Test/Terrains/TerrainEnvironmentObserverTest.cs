using System.Reflection;

using Engine.Core.Runtime;

using Game.Subsystems;
using Game.Terrains;
using Game.World;

namespace Survivalcraft.Core.Test.Terrains;

[Collection(TerrainEnvironmentClockCollection.Name)]
public sealed class TerrainEnvironmentObserverTest
{
    [Theory]
    [InlineData(0f, 0)]
    [InlineData(0.5f, -12)]
    [InlineData(0.625f, -24)]
    [InlineData(1f, 0)]
    public void SeasonSamplingPreservesTheCurveAndDoesNotInvalidateCpuInputs(float season, int temperature)
    {
        var previousStart = Time.FrameStartTime;
        var previousDuration = Time.FrameDuration;
        try
        {
            SetTime(1, 0.02f);
            using var terrain = new Terrain();
            var chunk = terrain.AllocateChunk(0, 0);
            var request = chunk.DerivedRevisions.Capture();
            terrain.SeasonTemperature = 100;
            terrain.SeasonHumidity = 100;
            var info = new SubsystemGameInfo { WorldSettings = new WorldSettings { TimeOfYear = season } };
            var sky = new SubsystemSky { SkyLightValue = 15 };
            var observer = new TerrainEnvironmentObserver(terrain, info);

            Assert.True(observer.Update());
            Assert.Equal(temperature, terrain.SeasonTemperature);
            Assert.Equal(0, terrain.SeasonHumidity);
            Assert.Equal(request, chunk.DerivedRevisions.Capture());
            Assert.False(observer.Update());

            sky.SkyLightValue = 7;
            info.WorldSettings.TimeOfYear = 0.625f;
            SetTime(1.5, 0.02f);
            Assert.False(observer.Update());
            Assert.Equal(temperature, terrain.SeasonTemperature);
        }
        finally { SetTime(previousStart, previousDuration); }
    }

    private static void SetTime(double start, float duration)
    {
        typeof(Time).GetProperty(nameof(Time.FrameStartTime), BindingFlags.Static | BindingFlags.Public)!
            .SetValue(null, start);
        typeof(Time).GetProperty(nameof(Time.FrameDuration), BindingFlags.Static | BindingFlags.Public)!
            .SetValue(null, duration);
    }
}
