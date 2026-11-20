using Engine.Core.Mathematics;

using Game.Rendering;

namespace Survivalcraft.Test.Terrains;

public sealed class ViewAtmosphereTest
{
    [Fact]
    public void HorizontalWeatherRaysHaveFiniteDensityAndDoNotDependOnAirScaling()
    {
        var atmosphere = new ViewAtmosphere(
            new Vector2(80f, 0.05f),
            new Vector3(-50f, 50f, 0.01f),
            1f,
            new Vector2(80f, 100f)
        );
        var offset = new Vector3(10f, 0f, 0f);
        Assert.Equal(0.15f, atmosphere.GetWeatherOpacity(offset), 5);
        Assert.Equal(
            atmosphere.GetWeatherOpacity(offset),
            (atmosphere with { VerticalDistanceScale = 4f }).GetWeatherOpacity(offset)
        );
        Assert.True(float.IsFinite(atmosphere.GetOpacity(offset)));
        Assert.Equal(0f, atmosphere.GetWeatherOpacity(Vector3.Zero));
    }

    [Theory]
    [InlineData(-0.00001f)]
    [InlineData(0f)]
    [InlineData(0.00001f)]
    public void WeatherIsContinuousThroughEqualHeight(float height)
    {
        var atmosphere = new ViewAtmosphere(default, new Vector3(-50f, 50f, 0.01f), 1f, new Vector2(80f, 100f));
        Assert.Equal(0.15f, atmosphere.GetWeatherOpacity(new Vector3(10f, height, 0f)), 5);
    }

    [Fact]
    public void EmptyOrDisabledWeatherProfilesRemainFinite()
    {
        var atmosphere = new ViewAtmosphere(default, new Vector3(0f, 0f, 1f), 1f, new Vector2(80f, 100f));
        Assert.Equal(0f, atmosphere.GetWeatherOpacity(new Vector3(10f, 0f, 0f)));
        Assert.Equal(
            0f,
            (atmosphere with { WeatherBottomTopDensity = new Vector3(-10f, 10f, 0f) }).GetWeatherOpacity(
                new Vector3(10f)
            )
        );
    }

    [Fact]
    public void AirWeatherAndCoverageComposeWithoutChangingTheirInputs()
    {
        var atmosphere = new ViewAtmosphere(
            new Vector2(80f, 0.05f),
            new Vector3(-50f, 50f, 0.001f),
            1f,
            new Vector2(80f, 100f)
        );
        var offset = new Vector3(90f, 0f, 0f);
        var air = atmosphere.GetDistanceOpacity(offset);
        var weather = atmosphere.GetWeatherOpacity(offset);
        var boundary = atmosphere.GetBoundaryOpacity(offset.XZ);
        Assert.Equal(1f - (1f - MathF.Max(air, boundary)) * (1f - weather), atmosphere.GetOpacity(offset), 5);
        Assert.Equal(1f, atmosphere.GetOpacity(new Vector3(100f, 0f, 0f)));
        Assert.Equal(0f, atmosphere.GetOpacity(Vector3.Zero));
    }
}
