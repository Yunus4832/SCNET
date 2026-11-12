using Game.Subsystems;
using Game.Terrains.Lighting;
using Game.World;

namespace Survivalcraft.Core.Test.Terrains;

public sealed class TerrainLightEnvironmentTest
{
    [Fact]
    public void DiscreteIntensityTablesPreserveEveryMoonPhase()
    {
        int[] moonless = [0, 3, 6, 9, 12, 15];
        int[] normal = [3, 5, 8, 10, 13, 15];
        for (var phase = 0; phase < 8; phase++)
        {
            for (var step = 0; step < 6; step++)
            {
                var light = TerrainLightEnvironment.FromIntensity(step / 5f, phase);
                Assert.Equal((phase == 4 ? moonless : normal)[step], light.SkyLightValue);
            }
        }
    }

    [Fact]
    public void CaptureReadsCurrentWorldClockWithoutWaitingForSkyUpdates()
    {
        var time = new SubsystemTimeOfDay
        {
            SubsystemGameInfo = new SubsystemGameInfo { WorldSettings = new WorldSettings() }
        };
        time.UpdateStarts();
        var sky = new SubsystemSky { SubsystemTimeOfDay = time };
        time.TimeOfDayOffset += time.Midday - time.CalculateTimeOfDay();
        sky.UpdateMoonPhase();
        sky.UpdateLightIntensity();
        Assert.Equal(15, sky.SkyLightValue);
        time.TimeOfDayOffset += time.Midnight - time.CalculateTimeOfDay();
        var night = TerrainLightEnvironment.Capture(time);
        Assert.InRange(night.SkyLightValue, 0, 3);
        Assert.Equal(15, sky.SkyLightValue);
        sky.UpdateMoonPhase();
        sky.UpdateLightIntensity();
        Assert.Equal(night.SkyLightValue, sky.SkyLightValue);
    }

    [Theory]
    [InlineData(-17.5)]
    [InlineData(-0.5)]
    [InlineData(0.499999)]
    [InlineData(0.5)]
    [InlineData(7.5)]
    [InlineData(8.5)]
    public void MoonPhasePreservesTheExistingDayBoundary(double day)
    {
        var expected = ((int)Math.Floor(day - 0.5 + 5.0) % 8 + 8) % 8;
        Assert.Equal(expected, TerrainLightEnvironment.CalculateMoonPhase(day));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(16)]
    public void InvalidDaylightIsRejected(int daylight)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TerrainLightEnvironment(daylight));
    }
}
